SELECT p."Id", p."Text", ei."Direction", ei."Citation"
FROM "Propositions" p
JOIN "EvidenceItems" ei ON ei."PropositionId" = p."Id"
JOIN "Claims" c ON c."Id" = p."ClaimId"
JOIN "Arguments" a ON a."Id" = c."ArgumentId"
WHERE a."Title" = 'Universal Basic Income'
ORDER BY p."Id", ei."Direction";