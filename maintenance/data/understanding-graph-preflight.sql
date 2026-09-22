SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.UnderstandingNodes', N'U') IS NULL
    THROW 51000, 'dbo.UnderstandingNodes does not exist.', 1;

IF OBJECT_ID(N'dbo.UnderstandingEdges', N'U') IS NULL
    THROW 51000, 'dbo.UnderstandingEdges does not exist.', 1;

IF OBJECT_ID(N'dbo.SocialArguments', N'U') IS NULL
    THROW 51000, 'dbo.SocialArguments does not exist.', 1;

DROP TABLE IF EXISTS #ResolvableNodes;
CREATE TABLE #ResolvableNodes
(
    Id int NOT NULL PRIMARY KEY
);

INSERT INTO #ResolvableNodes (Id)
SELECT node.Id
FROM dbo.UnderstandingNodes AS node
WHERE EXISTS
(
    SELECT 1
    FROM OPENJSON(CASE WHEN ISJSON(node.ArgumentIdsJson) = 1 THEN node.ArgumentIdsJson ELSE N'[]' END) AS argument_ref
    WHERE EXISTS
    (
        SELECT 1
        FROM dbo.SocialArguments AS direct_social
        WHERE direct_social.Id = TRY_CONVERT(uniqueidentifier, argument_ref.[value])
          AND direct_social.IsPublic = 1
    )
       OR EXISTS
    (
        SELECT 1
        FROM dbo.SocialArguments AS source_social
        WHERE source_social.SourceArgumentId = TRY_CONVERT(int, argument_ref.[value])
          AND source_social.IsPublic = 1
    )
);

SELECT
    (SELECT COUNT_BIG(*) FROM dbo.UnderstandingNodes) AS TotalNodes,
    (SELECT COUNT_BIG(*) FROM #ResolvableNodes) AS TransferableNodes,
    (SELECT COUNT_BIG(*) FROM dbo.UnderstandingNodes AS node WHERE ISJSON(node.ArgumentIdsJson) = 0) AS InvalidArgumentJsonNodes,
    (SELECT COUNT_BIG(*) FROM dbo.UnderstandingNodes AS node WHERE NOT EXISTS (SELECT 1 FROM #ResolvableNodes AS included WHERE included.Id = node.Id)) AS UnresolvedNodes,
    (SELECT COUNT_BIG(*)
     FROM dbo.UnderstandingEdges AS edge
     INNER JOIN #ResolvableNodes AS source_node ON source_node.Id = edge.SourceNodeId
     INNER JOIN #ResolvableNodes AS target_node ON target_node.Id = edge.TargetNodeId) AS TransferableEdges,
    (SELECT COUNT_BIG(*)
     FROM dbo.UnderstandingEdges AS edge
     LEFT JOIN dbo.UnderstandingNodes AS source_node ON source_node.Id = edge.SourceNodeId
     LEFT JOIN dbo.UnderstandingNodes AS target_node ON target_node.Id = edge.TargetNodeId
     WHERE source_node.Id IS NULL OR target_node.Id IS NULL) AS DanglingEdges;

SELECT
    node.Id,
    LEFT(node.CanonicalText, 160) AS CanonicalText,
    node.ArgumentIdsJson
FROM dbo.UnderstandingNodes AS node
WHERE NOT EXISTS
(
    SELECT 1
    FROM #ResolvableNodes AS included
    WHERE included.Id = node.Id
)
ORDER BY node.Id;