:on error exit

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    SET IDENTITY_INSERT dbo.Arguments ON;
:r .\seed_baseline_arguments.sql
    SET IDENTITY_INSERT dbo.Arguments OFF;

    DECLARE @RequiredArguments TABLE (Id int NOT NULL PRIMARY KEY);
    INSERT INTO @RequiredArguments (Id)
    VALUES (1), (3), (4), (6), (7), (8), (9), (10), (11),
           (12), (13), (15), (16), (17), (18), (20), (21), (22);

    IF EXISTS
    (
        SELECT 1
        FROM @RequiredArguments AS required
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.SocialArguments AS social
            INNER JOIN dbo.SocialPropositions AS proposition
                ON proposition.Id = social.ClaimPropositionId
            WHERE social.SourceArgumentId = required.Id
              AND social.IsPublic = 1
              AND social.IsShadowBanned = 0
        )
    )
        THROW 51000, 'Homepage link repair validation failed; transaction rolled back.', 1;

    COMMIT TRANSACTION;

    SELECT COUNT(*) AS VerifiedPublicMappings
    FROM @RequiredArguments AS required
    WHERE EXISTS
    (
        SELECT 1
        FROM dbo.SocialArguments AS social
        INNER JOIN dbo.SocialPropositions AS proposition
            ON proposition.Id = social.ClaimPropositionId
        WHERE social.SourceArgumentId = required.Id
          AND social.IsPublic = 1
          AND social.IsShadowBanned = 0
    );
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.Arguments OFF;
    END TRY
    BEGIN CATCH
    END CATCH;

    THROW;
END CATCH;