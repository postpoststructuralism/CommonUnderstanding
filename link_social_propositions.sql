-- ============================================================================
-- Link SocialArguments to their SocialPropositions via the join table
-- This is needed for the Understanding Graph sync to pick them up
-- ============================================================================

INSERT INTO "SocialArgumentPropositions" ("ArgumentId", "PropositionId", "Role", "OrderIndex")
SELECT 
    sa."Id",
    sa."ClaimPropositionId",
    0,  -- Claim role
    0   -- OrderIndex
FROM "SocialArguments" sa
WHERE sa."ClaimPropositionId" IS NOT NULL
  AND NOT EXISTS (
    SELECT 1 FROM "SocialArgumentPropositions" sap 
    WHERE sap."ArgumentId" = sa."Id" 
      AND sap."PropositionId" = sa."ClaimPropositionId"
  );

-- Report
SELECT 
    'Linked ' || COUNT(*) || ' SocialArguments to their propositions' AS result
FROM "SocialArgumentPropositions";