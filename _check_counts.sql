SELECT COUNT(*) AS total_nodes FROM "UnderstandingNodes";
SELECT COUNT(*) AS total_edges FROM "UnderstandingEdges";
SELECT COUNT(*) AS contradict_edges FROM "UnderstandingEdges" WHERE "Relationship" = 'contradicts';