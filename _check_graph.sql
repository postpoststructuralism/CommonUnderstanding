SELECT COUNT(*) AS node_count FROM "UnderstandingNodes";
SELECT COUNT(*) AS edge_count FROM "UnderstandingEdges";
SELECT COUNT(*) AS contradiction_count FROM "UnderstandingEdges" WHERE "Relationship" = 'contradicts';