# A Common Understanding — Multimodal Agency and Discovery Specification

Status: Proposed architecture and implementation contract for founder review.

Purpose: Consolidate the product vision into one specification for an initial coding-agent implementation followed by independent verification and refinement.

This document describes intended behaviour. It does not certify the current repository, security posture, or implementation status.

## 1. High-level summary

A Common Understanding is a multimodal environment for understanding yourself, connecting with others, and discovering how to build a fulfilling life together.

People participate organically: writing, speaking, listening, sharing creative work, joining conversations, meeting strangers, or hosting and watching livestreams. AI assistance is available throughout without having to dominate the experience. Users can invite agents to investigate a subject, check factual details, clarify a disagreement, facilitate a conversation, create something, or explore privately in depth.

As they choose what to retain, users build an inspectable personal dataset of experiences, interests, capabilities, needs, perspectives, and aspirations. They control what enters it, how it may be used, and what may be shared. The system adapts to their preferences and helps discover relationships, resources, activities, and opportunities that serve their own revisable goals.

The shared understanding map remains foundational, but displaying a graph is not the destination. The destination is greater agency, peaceful relationships, meaningful contribution, and genuine possibilities for living well together.

## 2. Elevator pitch

A Common Understanding is a social and AI environment that helps you discover and build the life you want.

Talk, create, livestream, meet people, or explore privately—in whatever way suits you. Bring an AI agent into a conversation to check a fact, investigate a question, or help work through a disagreement.

As you choose what to share and remember, you build a personal dataset you control. Your AI uses that understanding to help you find meaningful relationships, develop your abilities, pursue your aspirations, and discover what you can make possible with others.

## 3. Product principles

1. Agency over attention. Engagement serves user-chosen purposes; it is not the objective.
2. Many valid ways to participate. No mandatory profile questionnaire, social activity, modality, or self-analysis pathway.
3. Understanding without determinism. Observations and interpretations are contextual and revisable, not fixed personality labels.
4. Harmony without manufactured agreement. Cooperation, respectful disagreement, boundaries, and declining an encounter can all be worthwhile outcomes.
5. Contribution without instrumentalization. Encourage generosity without ranking human worth or requiring people to earn help.
6. Enjoyment is legitimate. Play, humour, companionship, artistic expression, and being heard need not justify themselves through productivity.
7. Transparency without coercion. Explain personalized invitations and proposed interpretations; make refusal easy.
8. User-controlled continuity. Retain information intentionally and use it only for authorized purposes.
9. Assistance by defined authority. Agents advise by default; any referee role requires a scoped agreement.
10. Safe abstention. No worthwhile suggestion, insufficient information, and uncertain findings are legitimate outputs.

“Long-term enlightened self-interest” is a guiding aspiration, not a hidden variable the AI is authorized to define. The system helps users investigate and pursue a good life; it does not prescribe one.

## 4. Scope and architectural position

Build a user-controlled understanding layer available through multiple interaction modes, with agents invocable in specific contexts.

Four connected layers:

| Layer | Responsibility |
|---|---|
| Personal understanding | Private observations, preferences, aspirations, evidence, interpretations, and permissions |
| Multimodal participation | Text, voice, video, media, live encounters, broadcasts, and private exploration |
| Invocable assistance | Investigation, fact-checking, clarification, facilitation, creation, and planning |
| Possibility discovery | Connections between people, knowledge, resources, capabilities, and user-chosen goals |

The architecture must distinguish private understanding, shared sessions, deliberately published expression, and authorized cooperation artifacts. Do not collapse them into one universally searchable profile or graph.

Reuse the existing application's framework, authentication, persistence, model integration, and test conventions after repository inspection. Do not introduce microservices, a new frontend framework, a new vector database, or an autonomous agent swarm without demonstrated necessity and founder approval.

Existing remediation and epistemic-integrity requirements remain prerequisites, not superseded instructions. Keep experimental functionality feature-flagged and disabled by default until relevant gates pass.

## 5. Primary experiences

### 5.1 Express

Publish ideas, questions, creative work, reflections, and media to an explicitly selected audience. Optional AI assistance may improve expression or suggest constructive invitations. Private storage, project inclusion, shared-map inclusion, and publication are separate actions.

### 5.2 Encounter

Meet someone for casual conversation, a different perspective, creative exchange, mutual learning, listening, or exploration.

Proposed flow:

1. Choose an intention or “surprise me.”
2. Set modality, language, approximate duration, boundaries, and AI preferences.
3. Choose shared interests, complementary differences, another perspective, or exploratory discovery.
4. Preview the encounter conditions and mutually accept.
5. Converse freely or use an optional opening prompt.
6. Leave immediately without explanation or penalty.
7. Optionally reconnect, reflect privately, or jointly share an outcome.

Do not claim a stranger is “good for you.” Explain the limited authorized basis of a suggestion.

### 5.3 Gather

Host or join livestreams, performances, discussions, and collaborative activities. Separate host, co-host, stage participant, audience member, and moderator permissions. Joining an audience is not consenting to appear on stage or in a broadcast.

### 5.4 Investigate

Explore a subject alone or with others. Agents can help formulate questions, find and assess sources through approved tools, separate factual and normative issues, compare interpretations, and identify unresolved questions.

### 5.5 Reflect and pursue

Explore an aspiration, examine its meaning, identify constraints and possibilities, investigate options, find resources or people, choose a small step, and revise after experience.

Goals remain optional and revisable. An expressed aspiration is not a commitment or obligation.

## 6. Agent participation contracts

### 6.1 Private investigation

The agent works for the individual within an authorized private scope. Results remain ephemeral unless retained by choice. The user may approve specific results as observations or shareable artifacts.

### 6.2 Shared investigation

An agent receives only the agreed shared context. All affected participants approve the applicable context-sharing policy. A single participant cannot authorize disclosure of others' private data.

Outputs distinguish sources, factual findings, interpretations, value judgments, uncertainties, and suggested next steps. Shared outputs must not expose private context through explanations or tailored wording.

### 6.3 Dispute assistance

Supported roles:

- Clarifier: identifies the actual disagreement.
- Investigator: checks a specific factual claim.
- Facilitator: helps participants communicate.
- Advisory assessor: evaluates a question under stated criteria.
- Referee: operates only under an explicit agreement naming the question, rules, scope, and consequences.

No default binding authority. No automated sanctions or external enforcement. Preserve disagreement and allow participants to challenge findings or withdraw from further assistance.

### 6.4 Per-invocation envelope

Each invocation specifies:

- Initiator and affected participants.
- Task and agent role.
- Authorized data scope and purpose.
- Shared versus private destination.
- Consent/grant references and current revisions.
- Permitted tools and cost limits.
- Retention policy.
- Authority limits.
- Model, prompt, policy, and source versions.

Recheck authorization before execution, persistence, and delivery. Fail closed when shared consent is missing or withdrawn. An invocation may be cancelled without turning cancellation into a personal inference.

## 7. User-controlled personal dataset

### 7.1 Control contract

Users can inspect retained information and provenance; distinguish their statements from generated interpretations; correct, reject, export, and delete; grant purpose-limited access; revoke future access; keep interactions temporary; and decide what informs personalization, discovery, or sharing.

Do not reuse private material for advertising or model training without separate explicit authorization. Provider configuration and contracts must support any privacy promises made by the interface.

### 7.2 Custody decision gate

“Exclusive control” requires an approved technical definition before release. Evaluate application-enforced access control, user-held encryption keys, or another custody model against authorized AI processing, recovery, and deletion requirements.

Do not promise zero-knowledge storage, end-to-end confidentiality from the operator, or exclusive user-held keys unless implemented and verified. State which processors can access plaintext and for what purpose. Unresolved custody semantics block strong marketing claims, not the drafting of a disabled prototype.

### 7.3 Perspective distinctions

- Self-description.
- Attributed external perspective.
- System interpretation.

A user confirming an interpretation does not erase its original provenance. “Others see me as…” remains a self-report unless separately supported. Third-party personal feedback ingestion is deferred pending an attribution, consent, and abuse design.

Schwartz or other value-framework outputs may support optional exploration. Preserve the deployed schema and explain its limits. Do not infer belief strength from classifier uncertainty or treat values as compatibility gates.

### 7.4 Retention

Ephemeral by default for new private investigations and live sessions. Retained observations are minimal and purpose-bound. Do not store a full transcript merely because a summary is useful.

Corrections and deletion invalidate dependent hypotheses, invitations, embeddings, indexes, caches, and pending jobs. Restore procedures reapply deletion records before restored data becomes available. Specify backup expiry and residual retention honestly.

A participant controls their own retained copy and authorized uses, not another person's independent memory or legitimate retained contribution. Explain shared-record limits before retention or publication.

## 8. Discovery and cooperation

### 8.1 Discovery loop

User-approved change or explicit request → resolve authorized scope → retrieve minimal context → propose hypotheses and invitations → validate provenance and disclosure → select a bounded set → explain purpose → receive explicit feedback → revise or invalidate.

Continuous discovery means bounded recomputation on relevant events, not continuous surveillance or unbounded model calls.

Invitations may include reflection, alternative perspectives, thought experiments, impact exploration, creative activities, encounters, gatherings, or practical opportunities. They must be worthwhile even when the user contributes no new data.

Provide “why this,” uncertainty, correction, skip, defer, and stop controls. Passive clicks, hesitation, and dismissal do not become sensitive personal observations.

### 8.2 Cooperation boundary

Cross-person matching uses deliberately approved artifacts, not pooled private profiles. A private agent may help draft an offer or request; only the approved revision crosses into the authorized discovery audience.

Artifacts can describe abilities, needs, aspirations, resources, interests, and conditions. Search for complementary differences and unexpected bridges as well as commonalities.

Each proposal identifies potential benefits and burdens for each participant, unknowns, conditions, and a reversible first step. No universal mutual-benefit percentage or generosity ledger.

Private information about Bob must not influence Alice's content, recommendation timing, or explanation unless Bob authorized an appropriate artifact. Do not launder private disclosure into supposedly generic prompts.

### 8.3 Opportunity lifecycle

Draft → OwnerApproved → EligibleForDiscovery → Proposed → InterestRecorded → MutuallyAuthorized → Active → Closed.

Also support Declined, Expired, Withdrawn, and Invalidated. Approval of an artifact does not authorize every introduction or release of contact details.

## 9. Multimodal and social permissions

Treat these permissions separately:

- Participate in a conversation.
- Activate camera or microphone.
- Appear in a broadcast or on stage.
- Record or transcribe.
- Supply session content to an AI processor.
- Persist private observations.
- Publish an excerpt, summary, or media object.
- Reuse material for another purpose.

Show active AI participation, recording, transcription, and broadcasting states persistently. A change in audience or processing policy requires fresh authorization before new processing begins.

Text, voice, video, uploaded media, and livestream content need explicit modality-specific input handling. Do not claim support for a modality because text summarization exists. Captions, transcripts, accessibility alternatives, and retention are distinct design choices and permissions.

## 10. Safety requirements

- Immediate leave, mute, block, and report.
- No automatic rematching after blocking.
- Pre-join camera/microphone preview; controlled activation.
- Rate limits, bounded queues, abuse handling, and host moderation.
- Audience and role controls for broadcasts.
- Clear public, group, and private labels.
- Reporting and appeal workflows appropriate to the pilot.
- User-supplied content treated as data, never privileged model instructions.

Proposed initial boundary: adult-only controlled pilot, subject to founder approval and an explicit age-assurance approach. Do not silently expand stranger encounters to minors.

Resolve incident evidence versus ephemeral participation explicitly. Specify retained safety metadata, user-submitted evidence, access, purpose, and expiry. Do not quietly record all conversations for moderation.

## 11. Logical data model

Adapt names and relationships to the repository after inspection.

| Record | Minimum responsibility |
|---|---|
| PersonalDataPolicy | Allowed purposes, domains, retention, processing, custody references |
| Observation | Owner, scope, perspective, statement, provenance, review state, permitted uses, expiry, revision |
| Aspiration | User-authored goal or exploration, constraints, optional next steps, revisions |
| ExplorationThread | Intention, branches, minimal retained state, expiry |
| AgentInvocation | Task envelope, authorization references, status, versions, bounded usage |
| AgentFinding | Sources, findings, interpretations, uncertainty, destination, retention |
| DiscoveryHypothesis | Tentative proposition, authorized source references, alternatives, status |
| DiscoveryInvitation | Type, purpose, content, explanation, source revisions, expiry |
| Expression | Author, content/media references, audience, publication revision |
| EncounterSession | Participants, modality, roles, consent state, lifecycle, minimal safety metadata |
| BroadcastSession | Host/stage/audience roles, publication and recording policies |
| SharingGrant | Exact artifact/revision, audience, purpose, expiry, withdrawal |
| CooperationArtifact | Approved offer, request, interest, or aspiration with conditions |
| OpportunityProposal | Approved inputs, benefits/burdens, unknowns, participant approvals |
| RelationshipPermission | Mutual reconnection and communication scope; block precedence |
| AuditEvent | Actor/process, action, purpose, object identifiers, versions, timestamp; no routine raw sensitive content |

Project-derived content retains its project restrictions. Personal ownership does not authorize extracting institutional or other participants' confidential material into a personal profile.

## 12. Implementation structure

Logical responsibilities:

- Context builder: minimal authorized context.
- Permission service: purpose, participant, audience, scope, revision, and expiry enforcement.
- Agent orchestrator: bounded invocation and structured output.
- Finding validator: source validation and epistemic distinctions.
- Observation service: review, retention, correction, export, deletion.
- Discovery service: bounded candidate selection and explanations.
- Session service: encounter and broadcast state transitions.
- Cooperation service: approved artifacts and mutual next steps.
- Invalidation service: correction, revocation, expiry, and deletion propagation.

These are responsibilities, not a mandate for separate services or infrastructure.

Model calls require structured schemas, authorized source-ID validation, bounded retries, timeouts, token/cost limits, provider allowlists, and safe failures. Models never set effective user identity or grant permissions. Rejected raw outputs must not enter ordinary logs. Explanations derive from authorized provenance, not hidden reasoning traces.

Realtime media integration must be selected after repository and deployment inspection. Assess data routing, privacy, participant limits, moderation hooks, accessibility, and operating cost. Do not invent an existing media implementation.

## 13. Initial vertical slice

Objective: prove natural participation, assistance on demand, and private continuity without automatic profiling.

1. Two consenting pilot users enter a bounded text encounter through a lightweight availability flow.
2. Either requests an agent investigation on a specific question.
3. Both approve the exact context supplied and the shared output destination.
4. The agent returns source-supported findings with factual/interpretive/value distinctions and uncertainty.
5. Each user may independently retain a minimal private result or observation.
6. A subsequent private investigation may use that retained information for an authorized purpose.
7. Neither private reflection affects the other user's experience or becomes a shared profile.
8. Both can leave, block, report, and optionally approve reconnection.

Text is the proposed first implemented modality, not the permanent product hierarchy. Establish modality-neutral contracts and clearly label unimplemented modes. Voice/video encounters and livestreaming follow as separate verified slices; no placeholder buttons implying working support.

Include feature flags, accessible text-first UI, deterministic test providers, model-unavailable states, scope explanations, correction, deletion, and consent revocation.

Do not implement broad public discovery, automated introductions, third-party personality reports, population-prevalence claims, binding automated adjudication, or comprehensive live moderation AI in this slice.

## 14. Acceptance tests

| Scenario | Required result |
|---|---|
| User skips onboarding | Can participate without a psychological profile |
| Generic invitation | Useful without fabricated personal evidence |
| One person invokes shared AI | No other participant's context sent before required approval |
| Consent withdrawn during a job | Execution/delivery rechecks permissions and suppresses unauthorized output |
| Model invents a source | Finding rejected or clearly excluded from sourced findings |
| No approved research tool available | No fabricated research or citations; report limitation |
| Fact versus value dispute | Distinction preserved; no manufactured factual verdict on a value preference |
| Referee requested without agreement | Advisory assistance only |
| Private reflection retained | Accessible only within the user's authorized scope |
| Another user's private data changes | Does not alter this user's discovery outputs |
| User rejects storage | No durable profiling record, transcript, or derived personalization persists |
| User corrects an observation | Dependent outputs invalidated before reuse |
| Sharing grant revoked | Pending discovery and delivery cannot use it |
| Block applied | No rematch; communication restrictions enforced server-side |
| AI provider unavailable | Core encounter, privacy controls, and retained-data inspection still work |
| Feature flag off | Existing workflows remain unchanged |
| Keyboard-only use | Can complete the initial slice |
| Deletion and restore | Derivatives removed/inactivated and deletion records reapplied before restored access |

Cover API/controller, service, job, cache, and race-condition paths. Use synthetic data and deterministic model responses for regression tests.

## 15. Coding-agent execution contract

### Pass 1: repository inspection and implementation

1. Read the repository's approved consolidated plan and instructions. Merge this feature into that source of truth; do not create a competing roadmap.
2. Map exact paths for authentication, project isolation, storage, model calls, jobs, publication, graph integration, realtime capabilities, tests, feature flags, and deployment triggers.
3. Record verified reuse, missing prerequisites, and unresolved decisions. Planning documents are not proof of implemented capabilities.
4. Identify release-blocking trust defects affecting this slice. Do not weaken existing remediation to ship the feature.
5. Implement the bounded slice behind disabled-by-default flags with focused tests.
6. Draft migrations for review; do not apply them to shared environments without approval.
7. Do not deploy, publish real user data, contact external parties, run expensive evaluations, or provision paid services without approval.
8. Stop at unresolved high-risk permission, custody, migration, or authority decisions; do not invent consent semantics.
9. Report changed paths, tests actually run, failures, screenshots where available, configuration requirements, assumptions, and unmet acceptance criteria.

“One-shot” means one coherent implementation attempt against this contract, not autonomous completion of the entire vision or automatic release.

### Pass 2: independent verification

Use a fresh verification context. Trace requirements to code and tests; attack cross-user and cross-project boundaries; race revocation against jobs; inspect disclosure through explanations and timing; verify rejection, correction, deletion, and block behaviour; and test whether the UI pressures users to disclose, agree, continue, or volunteer.

The verifier reports findings without redefining acceptance criteria. The implementation agent then fixes bounded findings. Founder approval remains required for scope, privacy risk, custody claims, schema application, moderation policy, authority semantics, and release.

## 16. Release gates and unresolved decisions

Before an external pilot, approve:

- Personal-data custody model and exact privacy claims.
- Model providers, retention settings, and processor permissions.
- Shared-session consent policy, including joins, departures, and context revisions.
- Incident evidence, retention, moderation, and appeals.
- Adult pilot admission and age-assurance policy.
- Research/source tooling and limitations.
- Media provider and deployment costs before adding voice/video.
- Accessibility and safety verification.
- Export/deletion/restore behaviour.

Do not certify legal compliance through this document. Any applicable legal and jurisdictional requirements require separate review before release.

## 17. Success criteria

Evaluate whether people report worthwhile participation, useful investigation, understandable agent findings, meaningful control over retained information, constructive connection, and progress toward self-chosen goals.

Also track privacy incidents, unwanted contact, correction rates, consent failures, unresolved reports, and operating cost. Collect only necessary operational data with transparent purposes.

Do not optimize session length, disclosure volume, agreement with the system, emotional arousal, generosity rankings, or accepted interpretations as proxies for human flourishing.

## 18. Final product contract

People can connect, create, play, investigate, disagree, contribute, and pursue aspirations in ways that suit them. AI is available to help, with defined permissions and limits. Personal understanding grows through intentional retention under user control. Shared understanding grows through deliberate contribution. Cooperation grows through mutually authorized possibilities—not extraction, compulsion, or manufactured agreement.
