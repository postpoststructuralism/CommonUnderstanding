-- Check source of nodes by status
SELECT "Status", 
       CASE WHEN "ArgumentIdsJson" IS NOT NULL AND "ArgumentIdsJson" != '[]' THEN 'Legacy' ELSE 'Social' END as source,
       count(*) 
FROM "UnderstandingNodes" 
GROUP BY "Status", source 
ORDER BY "Status", source;

-- Show some Unknown nodes with their confidence
SELECT "Id", "Status", "Confidence", LEFT("CanonicalText", 80) as text
FROM "UnderstandingNodes" 
WHERE "Status" = 3 
ORDER BY "Confidence" DESC 
LIMIT 10;

-- Show Settled nodes
SELECT "Id", "Status", "Confidence", LEFT("CanonicalText", 80) as text
FROM "UnderstandingNodes" 
WHERE "Status" = 1 
ORDER BY "Confidence" DESC 
LIMIT 10;

-- Show Unevaluated nodes
SELECT "Id", "Status", "Confidence", LEFT("CanonicalText", 80) as text
FROM "UnderstandingNodes" 
WHERE "Status" = 0 
ORDER BY "Confidence" DESC 
LIMIT 10;