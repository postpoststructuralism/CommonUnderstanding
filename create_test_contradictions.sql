-- ============================================================================
-- Test Data: Contradictions & Dialectical Syntheses
-- ============================================================================
-- This script creates test data that will trigger all three contradiction
-- detection strategies in UnderstandingGraphService.DetectContradictionsAsync:
--
--   1. Evidence direction (Supports + Opposes for same argument)
--   2. Social argument Contradicts links (ArgumentLink with LinkType=Contradicts)
--   3. Rebuttal propositions (SocialPropositionType=Rebuttal)
--
-- After inserting, run the pipeline to detect contradictions and generate syntheses.
-- This script is idempotent: it deletes existing test data first, then inserts fresh.
-- ============================================================================

BEGIN;

-- ============================================================================
-- Clean up any existing test data first (idempotency)
-- ============================================================================
DELETE FROM "EvidenceItems"
WHERE "PropositionId" IN (
    SELECT p."Id" FROM "Propositions" p
    JOIN "Claims" c ON c."Id" = p."ClaimId"
    JOIN "Arguments" a ON a."Id" = c."ArgumentId"
    WHERE a."Title" = 'Universal Basic Income'
);

DELETE FROM "Propositions"
WHERE "ClaimId" IN (
    SELECT c."Id" FROM "Claims" c
    JOIN "Arguments" a ON a."Id" = c."ArgumentId"
    WHERE a."Title" = 'Universal Basic Income'
);

DELETE FROM "Claims"
WHERE "ArgumentId" IN (
    SELECT "Id" FROM "Arguments" WHERE "Title" = 'Universal Basic Income'
);

DELETE FROM "Arguments" WHERE "Title" = 'Universal Basic Income';

-- Clean up social argument test data
DELETE FROM "SocialArgumentPropositions"
WHERE "ArgumentId" IN (
    'b0000000-0000-0000-0000-000000000001',
    'b0000000-0000-0000-0000-000000000002',
    'b0000000-0000-0000-0000-000000000003'
);

DELETE FROM "ArgumentLinks"
WHERE "Id" = 'c0000000-0000-0000-0000-000000000001';

DELETE FROM "SocialArguments"
WHERE "Id" IN (
    'b0000000-0000-0000-0000-000000000001',
    'b0000000-0000-0000-0000-000000000002',
    'b0000000-0000-0000-0000-000000000003'
);

DELETE FROM "SocialPropositions"
WHERE "Id" IN (
    'a0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000002',
    'a0000000-0000-0000-0000-000000000003',
    'a0000000-0000-0000-0000-000000000004',
    'a0000000-0000-0000-0000-000000000005',
    'a0000000-0000-0000-0000-000000000006'
);

-- ============================================================================
-- PART 1: Evidence direction strategy
-- Create a legacy Argument with two Propositions that have opposing evidence.
-- ============================================================================

-- Create an Argument
INSERT INTO "Arguments" ("Title", "RawText", "Status", "CreatedAt", "UpdatedAt", "SubmittedBy")
VALUES ('Universal Basic Income', 'Debate about whether UBI should be implemented.', 0, NOW(), NOW(), 'test-user');

-- Create a Claim for this argument
INSERT INTO "Claims" ("ArgumentId", "Text")
SELECT "Id", 'Universal Basic Income is a viable economic policy.'
FROM "Arguments" WHERE "Title" = 'Universal Basic Income';

-- Proposition 1: Supports UBI
INSERT INTO "Propositions" ("ClaimId", "Text", "Status", "ConfidenceScore", "EvidenceCount", "SortOrder")
SELECT "Id", 'Universal Basic Income reduces poverty and economic inequality.', 1, 0.85, 3, 1
FROM "Claims" WHERE "Text" = 'Universal Basic Income is a viable economic policy.';

-- Proposition 2: Opposes UBI
INSERT INTO "Propositions" ("ClaimId", "Text", "Status", "ConfidenceScore", "EvidenceCount", "SortOrder")
SELECT "Id", 'Universal Basic Income disincentivizes work and harms productivity.', 2, 0.75, 2, 2
FROM "Claims" WHERE "Text" = 'Universal Basic Income is a viable economic policy.';

-- Proposition 3: Different viewpoint, same argument context
INSERT INTO "Propositions" ("ClaimId", "Text", "Status", "ConfidenceScore", "EvidenceCount", "SortOrder")
SELECT "Id", 'Government budget allocation priorities should focus on infrastructure rather than direct cash transfers.', 2, 0.70, 1, 3
FROM "Claims" WHERE "Text" = 'Universal Basic Income is a viable economic policy.';

-- Evidence for Proposition 1 (Supports)
INSERT INTO "EvidenceItems" ("PropositionId", "Citation", "Direction", "Tier", "AddedBy", "AddedAt")
SELECT "Id", 'Study: UBI pilot programs show 15% reduction in poverty rates.', 0, 2, 'test-user', NOW()
FROM "Propositions" WHERE "Text" = 'Universal Basic Income reduces poverty and economic inequality.';

INSERT INTO "EvidenceItems" ("PropositionId", "Citation", "Direction", "Tier", "AddedBy", "AddedAt")
SELECT "Id", 'Meta-analysis of 20 UBI experiments shows improved mental health outcomes.', 0, 1, 'test-user', NOW()
FROM "Propositions" WHERE "Text" = 'Universal Basic Income reduces poverty and economic inequality.';

-- Evidence for Proposition 2 (Opposes)
INSERT INTO "EvidenceItems" ("PropositionId", "Citation", "Direction", "Tier", "AddedBy", "AddedAt")
SELECT "Id", 'Labor market study: UBI reduces workforce participation by 8%.', 1, 2, 'test-user', NOW()
FROM "Propositions" WHERE "Text" = 'Universal Basic Income disincentivizes work and harms productivity.';

INSERT INTO "EvidenceItems" ("PropositionId", "Citation", "Direction", "Tier", "AddedBy", "AddedAt")
SELECT "Id", 'Economic modeling shows UBI increases inflation without productivity gains.', 1, 3, 'test-user', NOW()
FROM "Propositions" WHERE "Text" = 'Universal Basic Income disincentivizes work and harms productivity.';

-- ============================================================================
-- PART 2: Social argument Contradicts link strategy
-- ============================================================================

-- SocialArgument A: Pro-regulation climate argument
INSERT INTO "SocialPropositions" ("Id", "Text", "Type", "UserId", "IsAIGenerated", "IsConfirmed", "CreatedAt")
VALUES ('a0000000-0000-0000-0000-000000000001', 'Climate change is primarily caused by human industrial activity and requires immediate government regulation.', 0, 'test-user', false, true, NOW());

INSERT INTO "SocialPropositions" ("Id", "Text", "Type", "UserId", "IsAIGenerated", "IsConfirmed", "CreatedAt")
VALUES ('a0000000-0000-0000-0000-000000000002', 'Global temperatures have risen 1.2C since pre-industrial times, correlating with CO2 emissions.', 1, 'test-user', false, true, NOW());

INSERT INTO "SocialArguments" ("Id", "Title", "ClaimPropositionId", "WarrantText", "Weight", "IsPublic", "IsShadowBanned", "UserId", "UpdatedAt", "UpvoteCount", "DownvoteCount", "HotScore", "WilsonScore", "ControversyScore", "IsAIValidated", "Tags", "SchwartzValues", "CreatedAt")
VALUES ('b0000000-0000-0000-0000-000000000001', 'Climate change requires government regulation', 'a0000000-0000-0000-0000-000000000001', 'The overwhelming scientific consensus supports human-caused climate change, and only coordinated government action can address it effectively.', 1.0, true, false, 'test-user', NOW(), 0, 0, 0.0, 0.0, 0.0, false, '{}', '{}', NOW());

INSERT INTO "SocialArgumentPropositions" ("ArgumentId", "PropositionId", "Role", "OrderIndex")
VALUES ('b0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000001', 0, 0);

INSERT INTO "SocialArgumentPropositions" ("ArgumentId", "PropositionId", "Role", "OrderIndex")
VALUES ('b0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000002', 1, 1);

-- SocialArgument B: Opposing climate argument
INSERT INTO "SocialPropositions" ("Id", "Text", "Type", "UserId", "IsAIGenerated", "IsConfirmed", "CreatedAt")
VALUES ('a0000000-0000-0000-0000-000000000003', 'Climate change is primarily a natural cyclical phenomenon, not caused by human activity.', 0, 'test-user', false, true, NOW());

INSERT INTO "SocialPropositions" ("Id", "Text", "Type", "UserId", "IsAIGenerated", "IsConfirmed", "CreatedAt")
VALUES ('a0000000-0000-0000-0000-000000000004', 'Historical climate data shows similar warming periods before the industrial revolution.', 1, 'test-user', false, true, NOW());

INSERT INTO "SocialArguments" ("Id", "Title", "ClaimPropositionId", "WarrantText", "Weight", "IsPublic", "IsShadowBanned", "UserId", "UpdatedAt", "UpvoteCount", "DownvoteCount", "HotScore", "WilsonScore", "ControversyScore", "IsAIValidated", "Tags", "SchwartzValues", "CreatedAt")
VALUES ('b0000000-0000-0000-0000-000000000002', 'Climate change is a natural cycle', 'a0000000-0000-0000-0000-000000000003', 'The Earth has always experienced climate cycles, and current changes are within natural variability.', 1.0, true, false, 'test-user', NOW(), 0, 0, 0.0, 0.0, 0.0, false, '{}', '{}', NOW());

INSERT INTO "SocialArgumentPropositions" ("ArgumentId", "PropositionId", "Role", "OrderIndex")
VALUES ('b0000000-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000003', 0, 0);

INSERT INTO "SocialArgumentPropositions" ("ArgumentId", "PropositionId", "Role", "OrderIndex")
VALUES ('b0000000-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000004', 1, 1);

-- Create the Contradicts link between argument A and argument B
INSERT INTO "ArgumentLinks" ("Id", "SourceArgumentId", "TargetArgumentId", "LinkType", "UserId", "CreatedAt")
VALUES ('c0000000-0000-0000-0000-000000000001', 'b0000000-0000-0000-0000-000000000001', 'b0000000-0000-0000-0000-000000000002', 1, 'test-user', NOW());

-- ============================================================================
-- PART 3: Rebuttal proposition strategy
-- ============================================================================

-- SocialArgument C with a Rebuttal proposition
INSERT INTO "SocialPropositions" ("Id", "Text", "Type", "UserId", "IsAIGenerated", "IsConfirmed", "CreatedAt")
VALUES ('a0000000-0000-0000-0000-000000000005', 'Renewable energy is too expensive and unreliable to replace fossil fuels.', 0, 'test-user', false, true, NOW());

INSERT INTO "SocialPropositions" ("Id", "Text", "Type", "UserId", "IsAIGenerated", "IsConfirmed", "CreatedAt")
VALUES ('a0000000-0000-0000-0000-000000000006', 'Renewable energy costs have dropped 90 percent in the last decade and grid-scale battery storage is now economically viable.', 3, 'test-user', false, true, NOW());

INSERT INTO "SocialArguments" ("Id", "Title", "ClaimPropositionId", "WarrantText", "Weight", "IsPublic", "IsShadowBanned", "UserId", "UpdatedAt", "UpvoteCount", "DownvoteCount", "HotScore", "WilsonScore", "ControversyScore", "IsAIValidated", "Tags", "SchwartzValues", "CreatedAt")
VALUES ('b0000000-0000-0000-0000-000000000003', 'Renewable energy is not viable', 'a0000000-0000-0000-0000-000000000005', 'Fossil fuels remain more cost-effective and reliable than intermittent renewable sources.', 1.0, true, false, 'test-user', NOW(), 0, 0, 0.0, 0.0, 0.0, false, '{}', '{}', NOW());

INSERT INTO "SocialArgumentPropositions" ("ArgumentId", "PropositionId", "Role", "OrderIndex")
VALUES ('b0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000005', 0, 0);

-- The rebuttal proposition is linked to the same argument
INSERT INTO "SocialArgumentPropositions" ("ArgumentId", "PropositionId", "Role", "OrderIndex")
VALUES ('b0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000006', 3, 1);

COMMIT;