SELECT DISTINCT ei."Direction"
FROM "EvidenceItems" ei
JOIN "Propositions" p ON p."Id" = ei."PropositionId"
JOIN "Claims" c ON c."Id" = p."ClaimId"
WHERE c."ArgumentId" = 50;