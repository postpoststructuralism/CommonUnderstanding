# Follow-up Arguments Implementation Plan

## Overview
Implement Twitter-like follow-up arguments functionality where users can respond to existing arguments with new arguments. This creates a threaded discussion structure similar to social media replies.

## Current State Analysis

### Existing Models
1. **SocialArgument**: Main social argument entity with voting, linking, and metadata
2. **ArgumentLink**: Typed directed edges between arguments (Supports, Contradicts, Refines, Extends)
3. **ArgumentVote**: User votes on arguments (upvote/downvote)
4. **SocialProposition**: Atomic propositions that make up arguments

### Current UI
- Feed view shows arguments with vote counts
- Detail view shows argument details and existing links
- Voting system with upvote/downvote buttons

## Implementation Requirements

### 1. Database Changes
- Add `ReplyCount` field to `SocialArgument` model to cache number of follow-up arguments
- Add `ParentArgumentId` field to `SocialArgument` for direct parent-child relationships
- OR: Enhance `ArgumentLink` with a new `LinkType.Reply` type
- Add database index on `ParentArgumentId` for efficient querying

### 2. Model Updates
**Option A: Enhance ArgumentLink (Preferred)**
- Add `LinkType.Reply` enum value
- Update `SocialArgument` to include `ReplyCount` denormalized field
- Add navigation property for replies: `ICollection<ArgumentLink> Replies`

**Option B: Add ParentArgumentId directly**
- Add `Guid? ParentArgumentId` to `SocialArgument`
- Add `ICollection<SocialArgument> Replies` navigation property
- Simpler queries but less flexible than typed links

**Recommended: Option A** - Reuse existing linking infrastructure

### 3. Service Layer Updates
- Create `FollowUpArgumentService` with methods:
  - `CreateFollowUpArgumentAsync(parentId, newArgument, userId)`
  - `GetFollowUpArgumentsAsync(parentId, skip, take)`
  - `UpdateReplyCountAsync(argumentId)`
- Update existing `ArgumentLink` service to handle reply links
- Add background job to periodically recalculate reply counts

### 4. API Endpoints
- `POST /api/social/arguments/{id}/follow-ups` - Create follow-up argument
- `GET /api/social/arguments/{id}/follow-ups` - Get paginated follow-ups
- `GET /api/social/arguments/{id}/follow-ups/count` - Get reply count

### 5. UI Components

#### Feed View Updates
- Add reply count badge next to vote buttons:
  ```
  [↑ 42] [↓ 15] [💬 8]  // Reply count icon
  ```

#### Detail View Updates
1. **Reply Form Section**
   - Text area for new follow-up argument
   - Character counter (max 1000 chars)
   - Submit button
   - Preview of argument structure

2. **Reply Thread Display**
   - Nested display of follow-up arguments
   - Indentation levels for deep threads
   - Collapsible threads
   - Pagination for long threads

3. **Reply Count Badge**
   - Display total replies in header
   - Click to scroll to reply section

### 6. Real-time Updates
- Use SignalR to update reply counts in real-time
- Notify parent argument author of new replies
- Update feed items with new reply counts

### 7. Validation Rules
- Maximum reply depth: 5 levels (prevent infinite nesting)
- Minimum argument quality: AI validation for new replies
- Rate limiting: 5 replies per hour per user
- No self-replies (can't reply to own argument)
- Content moderation for abusive replies

### 8. Notification System
- Notify parent argument author of new replies
- Optional: Notify users who voted on or commented on parent
- Email digest of reply activity

## Implementation Steps

### Phase 1: Database & Models (Day 1)
1. Add `LinkType.Reply` enum value
2. Add `ReplyCount` field to `SocialArgument`
3. Create database migration
4. Update `ApplicationDbContext` configuration

### Phase 2: Service Layer (Day 1-2)
1. Create `FollowUpArgumentService`
2. Update `ArgumentLinkService` to handle replies
3. Add reply count update logic
4. Create background job for count reconciliation

### Phase 3: API Endpoints (Day 2)
1. Add follow-up endpoints to `SocialArgumentController`
2. Implement validation and authorization
3. Add rate limiting middleware
4. Write API tests

### Phase 4: UI Components (Day 3)
1. Update feed view with reply count badges
2. Add reply form to detail view
3. Implement reply thread display
4. Add real-time updates via SignalR

### Phase 5: Testing & Polish (Day 4)
1. End-to-end testing
2. Performance testing with nested replies
3. UI/UX polish
4. Documentation

## Technical Details

### Database Schema Changes
```sql
-- Add ReplyCount column
ALTER TABLE "SocialArguments" ADD COLUMN "ReplyCount" integer NOT NULL DEFAULT 0;

-- Create index for efficient reply queries
CREATE INDEX "IX_ArgumentLinks_SourceId_LinkType" 
ON "ArgumentLinks" ("SourceArgumentId", "LinkType") 
WHERE "LinkType" = 'Reply';
```

### SignalR Events
```csharp
public interface IArgumentHub
{
    Task ReplyAdded(Guid parentArgumentId, Guid replyId);
    Task ReplyCountUpdated(Guid argumentId, int newCount);
}
```

### Rate Limiting
- Use `FixedWindowRateLimiter` for 5 replies/hour
- Store limits in Redis for distributed consistency
- Return `429 Too Many Requests` with retry-after header

## Success Metrics
1. **User Engagement**: Increase in time spent per session
2. **Content Generation**: More arguments created via replies
3. **Thread Depth**: Average reply depth > 1.5
4. **Quality**: AI validation score maintained > 0.7

## Risks & Mitigations
1. **Spam replies**: Implement AI content filtering and user reputation system
2. **Performance issues**: Paginate replies, cache counts, use materialized views
3. **Toxic discussions**: Content moderation tools, user blocking
4. **UI complexity**: Progressive disclosure, collapsible threads

## Dependencies
1. Existing SignalR infrastructure
2. AI validation service
3. User reputation system
4. Content moderation pipeline

## Future Enhancements
1. **Thread summarization**: AI-generated summaries of long threads
2. **Best reply highlighting**: Community-voted "most helpful" reply
3. **Reply notifications**: Customizable notification preferences
4. **Cross-thread linking**: Link replies to other arguments
5. **Media attachments**: Support images, links in replies