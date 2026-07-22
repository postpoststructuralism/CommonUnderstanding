-- ============================================================================
-- Publish all Phase 1 arguments to the social feed
-- Uses PL/pgSQL to properly link each Argument -> SocialProposition -> SocialArgument
-- ============================================================================

DO $$
DECLARE
    arg_record RECORD;
    claim_text TEXT;
    new_prop_id UUID;
BEGIN
    -- Step 1: Update all non-Complete arguments to Complete
    UPDATE "Arguments"
    SET "Status" = 'Complete',
        "UpdatedAt" = NOW()
    WHERE "Status" != 'Complete';

    RAISE NOTICE 'Updated arguments to Complete.';

    -- Step 2: For each argument without a SocialArgument, create SocialProposition + SocialArgument
    FOR arg_record IN
        SELECT a."Id", a."Title", a."RawText", a."SubmittedBy"
        FROM "Arguments" a
        WHERE a."Id" NOT IN (
            SELECT "SourceArgumentId" FROM "SocialArguments" WHERE "SourceArgumentId" IS NOT NULL
        )
    LOOP
        -- Get claim text or fall back to title
        SELECT "Text" INTO claim_text
        FROM "Claims"
        WHERE "Claims"."ArgumentId" = arg_record."Id"
        LIMIT 1;

        IF claim_text IS NULL OR claim_text = '' THEN
            claim_text := arg_record."Title";
        END IF;

        -- Create SocialProposition
        new_prop_id := gen_random_uuid();
        INSERT INTO "SocialPropositions" ("Id", "Text", "Type", "UserId", "IsAIGenerated", "IsConfirmed", "CreatedAt")
        VALUES (new_prop_id, claim_text, 0, arg_record."SubmittedBy", true, true, NOW());

        -- Create SocialArgument
        INSERT INTO "SocialArguments" (
            "Id", "Title", "ClaimPropositionId", "WarrantText",
            "UserId", "SourceArgumentId", "IsPublic", "IsShadowBanned",
            "Weight", "UpvoteCount", "DownvoteCount", "HotScore", "WilsonScore", "ControversyScore",
            "IsAIValidated", "Tags", "SchwartzValues", "ReplyCount", "CreatedAt", "UpdatedAt"
        )
        VALUES (
            gen_random_uuid(),
            arg_record."Title",
            new_prop_id,
            arg_record."RawText",
            arg_record."SubmittedBy",
            arg_record."Id",
            true,   -- IsPublic
            false,  -- IsShadowBanned
            1.0,    -- Weight
            0, 0, 0.0, 0.0, 0.0,
            false,
            ARRAY[]::text[],
            ARRAY[]::text[],
            0,
            NOW(),
            NOW()
        );
    END LOOP;

    RAISE NOTICE 'Done. Published % arguments.', (SELECT COUNT(*) FROM "SocialArguments" WHERE "IsPublic" = true);
END $$;