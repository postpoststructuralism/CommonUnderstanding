SELECT "Id", "CanonicalText", "SemanticEmbedding" IS NOT NULL AS has_embedding
FROM "UnderstandingNodes"
WHERE "Id" >= 176
ORDER BY "Id";