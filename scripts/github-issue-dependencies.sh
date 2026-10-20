#!/usr/bin/env bash
set -Eeuo pipefail

readonly REPO="postpoststructuralism/CommonUnderstanding"
readonly PAIRS=(
  20:9 25:9 29:9 38:9 39:9
  21:20 22:20 23:20 24:20 24:22 31:20
  26:25
  30:29
  33:32 34:32 35:33 36:33
  37:41 42:41 43:41 43:11 44:41 44:42 45:44 46:24
  47:7 47:20 47:21 47:22 47:25 47:26 47:28
  48:47 48:35 48:42 48:43 48:44 48:45 48:46
)

usage() {
  printf 'Usage: %s [--apply]\n' "${0##*/}"
  printf 'Without --apply, validate and preview only.\n'
}

apply=false
case "${1:-}" in
  "") ;;
  --apply) apply=true ;;
  --help|-h) usage; exit 0 ;;
  *) usage >&2; exit 2 ;;
esac
if (( $# > 1 )); then
  usage >&2
  exit 2
fi

for command_name in gh; do
  if ! command -v "$command_name" >/dev/null 2>&1; then
    printf 'Required command not found: %s\n' "$command_name" >&2
    exit 1
  fi
done
gh auth status >/dev/null

declare -A issue_ids=()
declare -A known_pairs=()
declare -A existing_pairs=()
declare -A checked_blocked_issues=()
declare -A indegree=()
declare -A dependents=()
nodes=()

# Validate the complete map before making any API writes.
for pair in "${PAIRS[@]}"; do
  if [[ ! "$pair" =~ ^([0-9]+):([0-9]+)$ ]]; then
    printf 'Invalid dependency pair: %s\n' "$pair" >&2
    exit 1
  fi
  blocked="${BASH_REMATCH[1]}"
  blocker="${BASH_REMATCH[2]}"
  if [[ "$blocked" == "$blocker" ]]; then
    printf 'Issue #%s cannot block itself.\n' "$blocked" >&2
    exit 1
  fi
  if [[ -n "${known_pairs[$pair]:-}" ]]; then
    printf 'Duplicate dependency pair: %s\n' "$pair" >&2
    exit 1
  fi
  known_pairs[$pair]=1
  blocked="${BASH_REMATCH[1]}"
  blocker="${BASH_REMATCH[2]}"
  if [[ -z "${indegree[$blocked]+present}" ]]; then
    indegree[$blocked]=0
    nodes+=("$blocked")
  fi
  if [[ -z "${indegree[$blocker]+present}" ]]; then
    indegree[$blocker]=0
    nodes+=("$blocker")
  fi
  dependents[$blocker]="${dependents[$blocker]:-} $blocked"
  indegree[$blocked]=$((indegree[$blocked] + 1))
done

# Reject cycles before looking up issues or making API writes.
queue=()
for number in "${nodes[@]}"; do
  if (( indegree[$number] == 0 )); then
    queue+=("$number")
  fi
done
processed=0
for (( queue_index = 0; queue_index < ${#queue[@]}; queue_index++ )); do
  number="${queue[$queue_index]}"
  processed=$((processed + 1))
  for dependent in ${dependents[$number]:-}; do
    indegree[$dependent]=$((indegree[$dependent] - 1))
    if (( indegree[$dependent] == 0 )); then
      queue+=("$dependent")
    fi
  done
done
if (( processed != ${#nodes[@]} )); then
  printf 'Dependency map contains a cycle; no GitHub changes were made.\n' >&2
  exit 1
fi

# Resolve and verify each distinct issue number, including pull request exclusion.
for pair in "${PAIRS[@]}"; do
  blocked="${pair%%:*}"
  blocker="${pair##*:}"
  for number in "$blocked" "$blocker"; do
    if [[ -n "${issue_ids[$number]:-}" ]]; then
      continue
    fi
    record=$(gh api "repos/$REPO/issues/$number" \
      --jq '[.id, .number, (.pull_request != null)] | @tsv')
    IFS=$'\t' read -r database_id actual_number is_pull_request <<< "$record"
    if [[ ! "$database_id" =~ ^[0-9]+$ || "$actual_number" != "$number" ]]; then
      printf 'Could not validate issue #%s (API response: %s).\n' "$number" "$record" >&2
      exit 1
    fi
    if [[ "$is_pull_request" == true ]]; then
      printf '#%s is a pull request, not an issue.\n' "$number" >&2
      exit 1
    fi
    issue_ids[$number]="$database_id"
  done
done

# Read current dependencies for every target once so reruns are idempotent.
for pair in "${PAIRS[@]}"; do
  blocked="${pair%%:*}"
  if [[ -n "${checked_blocked_issues[$blocked]:-}" ]]; then
    continue
  fi
  current_blockers=$(gh api --paginate \
    "repos/$REPO/issues/$blocked/dependencies/blocked_by" --jq '.[].number')
  while IFS= read -r current_blocker; do
    if [[ "$current_blocker" =~ ^[0-9]+$ ]]; then
      existing_pairs["$blocked:$current_blocker"]=1
    fi
  done <<< "$current_blockers"
  checked_blocked_issues[$blocked]=1
done

printf 'Preflight passed for %d dependency pairs in %s.\n' "${#PAIRS[@]}" "$REPO"
for pair in "${PAIRS[@]}"; do
  blocked="${pair%%:*}"
  blocker="${pair##*:}"
  if [[ -n "${existing_pairs[$pair]:-}" ]]; then
    printf 'Already present: #%s blocked by #%s\n' "$blocked" "$blocker"
  elif [[ "$apply" == true ]]; then
    printf 'Will add: #%s blocked by #%s\n' "$blocked" "$blocker"
  else
    printf 'Would add: #%s blocked by #%s\n' "$blocked" "$blocker"
  fi
done

if [[ "$apply" != true ]]; then
  printf '\nPreview only. Re-run with --apply to add missing dependencies.\n'
  exit 0
fi

for pair in "${PAIRS[@]}"; do
  if [[ -n "${existing_pairs[$pair]:-}" ]]; then
    continue
  fi
  blocked="${pair%%:*}"
  blocker="${pair##*:}"
  gh api -X POST "repos/$REPO/issues/$blocked/dependencies/blocked_by" \
    -F "issue_id=${issue_ids[$blocker]}" >/dev/null
  printf 'Added: #%s blocked by #%s\n' "$blocked" "$blocker"
done