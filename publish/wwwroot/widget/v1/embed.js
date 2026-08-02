/**
 * Common Understanding Widget — Embeddable Comments SDK
 * Version 1.0
 *
 * Usage:
 *   <div id="cu-widget" data-page-url="https://example.com/article"></div>
 *   <script src="https://yourdomain.com/widget/v1/embed.js"></script>
 */
(function (global) {
    'use strict';

    const API_BASE = '/api/widget';
    const HUB_PATH = '/hubs/widget';

    let signalRConnection = null;
    let currentState = {
        siteId: null,
        threadId: null,
        comments: [],
        sort: 'hot',
        page: 0,
        hasMore: true,
        loading: false
    };

    // ── Utility ──────────────────────────────────────────────────────────────

    function getElement(selector) {
        return typeof selector === 'string'
            ? document.querySelector(selector)
            : selector;
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    function timeAgo(dateStr) {
        const now = Date.now();
        const then = new Date(dateStr).getTime();
        const seconds = Math.floor((now - then) / 1000);
        if (seconds < 60) return 'just now';
        const minutes = Math.floor(seconds / 60);
        if (minutes < 60) return minutes + 'm ago';
        const hours = Math.floor(minutes / 60);
        if (hours < 24) return hours + 'h ago';
        const days = Math.floor(hours / 24);
        if (days < 30) return days + 'd ago';
        return new Date(dateStr).toLocaleDateString();
    }

    // ── API Calls ────────────────────────────────────────────────────────────

    async function apiGet(path) {
        const resp = await fetch(API_BASE + path);
        if (!resp.ok) throw new Error('API error: ' + resp.status);
        return resp.json();
    }

    async function apiPost(path, body) {
        const resp = await fetch(API_BASE + path, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!resp.ok) throw new Error('API error: ' + resp.status);
        return resp.json();
    }

    // ── SignalR ──────────────────────────────────────────────────────────────

    async function connectSignalR(threadId) {
        if (typeof signalR === 'undefined') {
            console.warn('[CU Widget] SignalR not available, real-time disabled');
            return;
        }

        try {
            signalRConnection = new signalR.HubConnectionBuilder()
                .withUrl(HUB_PATH)
                .withAutomaticReconnect()
                .build();

            signalRConnection.on('newComment', (comment) => {
                onNewComment(comment);
            });

            signalRConnection.on('voteUpdate', (data) => {
                onVoteUpdate(data);
            });

            signalRConnection.on('error', (msg) => {
                console.warn('[CU Widget] Server error:', msg);
            });

            await signalRConnection.start();
            await signalRConnection.invoke('SubscribeToThread', threadId);
        } catch (err) {
            console.warn('[CU Widget] SignalR connection failed:', err);
        }
    }

    // ── Event Handlers ───────────────────────────────────────────────────────

    function onNewComment(comment) {
        currentState.comments.unshift(comment);
        renderComments();
        updateCount();
    }

    function onVoteUpdate(data) {
        // In a full implementation, update the specific comment's score
    }

    // ── Rendering ────────────────────────────────────────────────────────────

    function renderWidget(container, config) {
        container.innerHTML = `
            <div class="cu-widget">
                <div class="cu-header">
                    ${config.logoUrl ? `<img class="cu-logo" src="${escapeHtml(config.logoUrl)}" alt="Logo">` : ''}
                    <span class="cu-title">Comments</span>
                    <span class="cu-count" id="cu-count">0</span>
                </div>
                ${config.isLocked
                    ? '<div class="cu-locked">Comments are closed.</div>'
                    : `
                    <div class="cu-composer">
                        <textarea class="cu-textarea" id="cu-textarea" placeholder="Share your thoughts..." rows="3"></textarea>
                        <div class="cu-composer-actions">
                            <button class="cu-btn cu-btn-primary" id="cu-submit">Post Comment</button>
                        </div>
                    </div>
                    `
                }
                <div class="cu-sort-bar">
                    <button class="cu-sort-btn active" data-sort="hot">Hot</button>
                    <button class="cu-sort-btn" data-sort="new">New</button>
                    <button class="cu-sort-btn" data-sort="top">Top</button>
                    <button class="cu-sort-btn" data-sort="controversial">Controversial</button>
                </div>
                <div class="cu-comments" id="cu-comments">
                    <div class="cu-loading">Loading comments...</div>
                </div>
                <div class="cu-footer">
                    <span class="cu-powered">Powered by <a href="https://commonunderstanding.com" target="_blank">Common Understanding</a></span>
                </div>
            </div>
        `;

        // Attach event listeners
        const submitBtn = container.querySelector('#cu-submit');
        if (submitBtn) {
            submitBtn.addEventListener('click', () => submitComment(container));
        }

        const textarea = container.querySelector('#cu-textarea');
        if (textarea) {
            textarea.addEventListener('keydown', (e) => {
                if (e.ctrlKey && e.key === 'Enter') {
                    e.preventDefault();
                    submitComment(container);
                }
            });
        }

        container.querySelectorAll('.cu-sort-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                container.querySelectorAll('.cu-sort-btn').forEach(b => b.classList.remove('active'));
                btn.classList.add('active');
                currentState.sort = btn.dataset.sort;
                currentState.page = 0;
                currentState.comments = [];
                loadComments(container);
            });
        });

        // Infinite scroll
        const commentsEl = container.querySelector('#cu-comments');
        commentsEl.addEventListener('scroll', () => {
            if (commentsEl.scrollTop + commentsEl.clientHeight >= commentsEl.scrollHeight - 200) {
                loadComments(container);
            }
        });
    }

    async function loadComments(container) {
        if (currentState.loading || !currentState.hasMore) return;
        currentState.loading = true;

        const commentsEl = container.querySelector('#cu-comments');

        try {
            const comments = await apiGet(
                `/${currentState.siteId}/threads/${currentState.threadId}/comments` +
                `?sort=${currentState.sort}&skip=${currentState.page * 50}&take=50`
            );

            if (comments.length < 50) currentState.hasMore = false;
            currentState.comments.push(...comments);
            currentState.page++;
            renderComments(container);
            updateCount(container);
        } catch (err) {
            commentsEl.innerHTML = '<div class="cu-error">Failed to load comments. Please try again.</div>';
        } finally {
            currentState.loading = false;
        }
    }

    function renderComments(container) {
        const commentsEl = container ? container.querySelector('#cu-comments') : document.querySelector('#cu-comments');
        if (!commentsEl) return;

        if (currentState.comments.length === 0) {
            commentsEl.innerHTML = '<div class="cu-empty">No comments yet. Be the first to share your thoughts!</div>';
            return;
        }

        commentsEl.innerHTML = currentState.comments.map(comment => `
            <div class="cu-comment ${comment.isDeleted ? 'cu-deleted' : ''}" data-id="${escapeHtml(comment.id)}">
                <div class="cu-comment-header">
                    <span class="cu-author">${escapeHtml(comment.authorName)}</span>
                    <span class="cu-time">${timeAgo(comment.createdAt)}</span>
                </div>
                <div class="cu-comment-body">
                    ${comment.isDeleted ? '<em>[Comment removed]</em>' : escapeHtml(comment.content)}
                </div>
                <div class="cu-comment-actions">
                    <button class="cu-vote-btn" data-vote="up" title="Upvote">▲ <span class="cu-score">${comment.upvotes}</span></button>
                    <button class="cu-vote-btn" data-vote="down" title="Downvote">▼ <span class="cu-score">${comment.downvotes}</span></button>
                    <button class="cu-reply-btn" data-id="${escapeHtml(comment.id)}">Reply</button>
                    ${comment.wilsonScore !== null ? `<span class="cu-quality">${(comment.wilsonScore * 100).toFixed(0)}% quality</span>` : ''}
                </div>
            </div>
        `).join('');

        // Attach vote and reply handlers
        commentsEl.querySelectorAll('.cu-vote-btn').forEach(btn => {
            btn.addEventListener('click', () => vote(btn.dataset.vote, btn.closest('.cu-comment').dataset.id));
        });

        commentsEl.querySelectorAll('.cu-reply-btn').forEach(btn => {
            btn.addEventListener('click', () => showReplyForm(btn.dataset.id));
        });
    }

    function updateCount(container) {
        const el = container ? container.querySelector('#cu-count') : document.querySelector('#cu-count');
        if (el) el.textContent = currentState.comments.length;
    }

    async function submitComment(container) {
        const textarea = container.querySelector('#cu-textarea');
        const content = textarea.value.trim();
        if (!content) return;

        textarea.disabled = true;

        try {
            if (signalRConnection && signalRConnection.state === 'Connected') {
                await signalRConnection.invoke('PostComment', currentState.siteId, currentState.threadId, content, null);
            } else {
                await apiPost(`/${currentState.siteId}/threads/${currentState.threadId}/comments`, { content });
                // Reload to get the new comment
                currentState.page = 0;
                currentState.comments = [];
                await loadComments(container);
            }
            textarea.value = '';
        } catch (err) {
            console.error('[CU Widget] Failed to post comment:', err);
        } finally {
            textarea.disabled = false;
            textarea.focus();
        }
    }

    async function vote(direction, argumentId) {
        try {
            if (signalRConnection && signalRConnection.state === 'Connected') {
                await signalRConnection.invoke('Vote', currentState.siteId, argumentId, direction);
            } else {
                await apiPost(`/${currentState.siteId}/comments/${argumentId}/vote?direction=${direction}`, {});
            }
        } catch (err) {
            console.error('[CU Widget] Vote failed:', err);
        }
    }

    function showReplyForm(parentId) {
        // Simple inline reply — in production, use a proper form
        const parent = document.querySelector(`.cu-comment[data-id="${parentId}"]`);
        if (!parent) return;

        const existing = parent.querySelector('.cu-reply-form');
        if (existing) {
            existing.remove();
            return;
        }

        const form = document.createElement('div');
        form.className = 'cu-reply-form';
        form.innerHTML = `
            <textarea class="cu-textarea" placeholder="Write a reply..." rows="2"></textarea>
            <button class="cu-btn cu-btn-primary cu-reply-submit">Reply</button>
            <button class="cu-btn cu-reply-cancel">Cancel</button>
        `;

        form.querySelector('.cu-reply-submit').addEventListener('click', async () => {
            const replyText = form.querySelector('textarea').value.trim();
            if (!replyText) return;

            try {
                if (signalRConnection && signalRConnection.state === 'Connected') {
                    await signalRConnection.invoke('PostComment', currentState.siteId, currentState.threadId, replyText, parentId);
                } else {
                    await apiPost(`/${currentState.siteId}/threads/${currentState.threadId}/comments`, {
                        content: replyText,
                        parentArgumentId: parentId
                    });
                }
                form.remove();
            } catch (err) {
                console.error('[CU Widget] Reply failed:', err);
            }
        });

        form.querySelector('.cu-reply-cancel').addEventListener('click', () => form.remove());
        parent.appendChild(form);
    }

    // ── Initialization ───────────────────────────────────────────────────────

    async function initWidget(container) {
        const pageUrl = container.dataset.pageUrl || window.location.href;
        const siteId = container.dataset.siteId;

        if (!siteId) {
            console.error('[CU Widget] Missing data-site-id attribute');
            return;
        }

        currentState.siteId = siteId;

        try {
            const config = await apiGet(`/${siteId}/config?pageUrl=${encodeURIComponent(pageUrl)}`);
            currentState.threadId = config.threadId;

            renderWidget(container, config);
            await loadComments(container);
            await connectSignalR(config.threadId);
        } catch (err) {
            container.innerHTML = '<div class="cu-widget cu-error">Unable to load comments.</div>';
            console.error('[CU Widget] Initialization failed:', err);
        }
    }

    // ── Auto-initialize ──────────────────────────────────────────────────────

    function autoInit() {
        document.querySelectorAll('#cu-widget, [data-cu-widget]').forEach(el => {
            initWidget(el);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', autoInit);
    } else {
        autoInit();
    }

    // Expose for manual initialization
    global.CUWidget = {
        init: initWidget,
        version: '1.0.0'
    };

})(window);