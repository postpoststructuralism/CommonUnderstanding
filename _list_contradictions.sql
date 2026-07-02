SELECT e."Id", e."SourceNodeId", sn."CanonicalText" AS source_text,
       e."TargetNodeId", tn."CanonicalText" AS target_text,
       e."Relationship", e."Weight", e."ProvenanceJson"
FROM "UnderstandingEdges" e
JOIN "UnderstandingNodes" sn ON e."SourceNodeId" = sn."Id"
JOIN "UnderstandingNodes" tn ON e."TargetNodeId" = tn."Id"
WHERE e."Relationship" = 'contradicts';