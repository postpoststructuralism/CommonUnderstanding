SELECT "Weight", COUNT(*) as cnt 
FROM "UnderstandingEdges" 
WHERE "Weight" >= 0.6 
GROUP BY "Weight" 
ORDER BY "Weight" DESC 
OFFSET 20;