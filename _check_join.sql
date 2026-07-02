SELECT p."Id", p."Text", p."ClaimId", c."Id" as claim_id, c."ArgumentId", a."Id" as arg_id, a."Title"
FROM "Propositions" p
JOIN "Claims" c ON c."Id" = p."ClaimId"
JOIN "Arguments" a ON a."Id" = c."ArgumentId"
WHERE a."Title" = 'Universal Basic Income';