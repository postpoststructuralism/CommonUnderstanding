SELECT ds."Id", ds."SynthesisNodeId", n."CanonicalText" AS synthesis_text,
       ds."ParentNodeIdsJson", ds."CreatedAt"
FROM "DialecticalSyntheses" ds
JOIN "UnderstandingNodes" n ON ds."SynthesisNodeId" = n."Id"
ORDER BY ds."Id" DESC
LIMIT 10;