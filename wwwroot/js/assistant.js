(function () {
    "use strict";

    const chatLog = document.getElementById("chatLog");
    const chatForm = document.getElementById("chatForm");
    const chatInput = document.getElementById("chatInput");
    const taskTableBody = document.getElementById("taskTableBody");
    const taskCount = document.getElementById("taskCount");
    const refreshBtn = document.getElementById("refreshBtn");
    const confirmBar = document.getElementById("confirmBar");
    const confirmText = document.getElementById("confirmText");
    const confirmYes = document.getElementById("confirmYes");
    const confirmNo = document.getElementById("confirmNo");
    const prevPageBtn = document.getElementById("prevPageBtn");
    const nextPageBtn = document.getElementById("nextPageBtn");
    const pageLabel = document.getElementById("pageLabel");
    const welcomePanel = document.getElementById("welcomePanel");
    const newChatBtn = document.getElementById("newChatBtn");
    const auditLog = document.getElementById("auditLog");

    const PAGE_SIZE = 5;
    let allTasks = [];
    let currentPage = 1;
    const MAX_AUDIT_ENTRIES = 25;

    const CONVERSATION_KEY = "taskAssistant.conversationId";

    function newConversationId() {
        const id = crypto.randomUUID();
        try {
            sessionStorage.setItem(CONVERSATION_KEY, id);
        } catch {
            /* sessionStorage unavailable (private mode etc.) - id still works for this page load */
        }
        return id;
    }

    // Persist a conversation id per browser tab so the assistant can resolve short follow-ups
    // like "actually make that due tomorrow" without the user repeating the task name.
    let conversationId = (() => {
        try {
            return sessionStorage.getItem(CONVERSATION_KEY) || newConversationId();
        } catch {
            return newConversationId();
        }
    })();

    let pendingDeleteId = null;

    // Small inline icon set so each outcome reads at a glance, not just from the text label.
    const STATUS_ICONS = {
        success: '<svg viewBox="0 0 20 20" fill="none"><path d="M4 10.5l4 4 8-9" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        rejected: '<svg viewBox="0 0 20 20" fill="none"><path d="M6 6l8 8M14 6l-8 8" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>',
        not_found: '<svg viewBox="0 0 20 20" fill="none"><circle cx="10" cy="10" r="7" stroke="currentColor" stroke-width="2"/><path d="M7.5 7.5l5 5M12.5 7.5l-5 5" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
        invalid_arguments: '<svg viewBox="0 0 20 20" fill="none"><path d="M6 6l8 8M14 6l-8 8" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>',
        error: '<svg viewBox="0 0 20 20" fill="none"><path d="M6 6l8 8M14 6l-8 8" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>',
        ambiguous: '<svg viewBox="0 0 20 20" fill="none"><path d="M10 3l8 14H2z" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/><path d="M10 8v4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/><circle cx="10" cy="14.3" r="0.9" fill="currentColor"/></svg>',
        confirmation_required: '<svg viewBox="0 0 20 20" fill="none"><path d="M10 3l8 14H2z" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/><path d="M10 8v4" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/><circle cx="10" cy="14.3" r="0.9" fill="currentColor"/></svg>',
    };

    function addBubble(role, text, status) {
        const row = document.createElement("div");
        row.className = "chat-row " + role;

        if (role === "assistant") {
            const avatar = document.createElement("div");
            avatar.className = "avatar";
            avatar.setAttribute("aria-hidden", "true");
            avatar.textContent = "AI";
            row.appendChild(avatar);
        }

        const bubble = document.createElement("div");
        bubble.className = "chat-bubble " + role + (status ? " status-" + status : "");

        if (role === "assistant" && status) {
            const tag = document.createElement("span");
            tag.className = "action-tag";
            if (STATUS_ICONS[status]) {
                const icon = document.createElement("span");
                icon.className = "status-icon";
                icon.innerHTML = STATUS_ICONS[status];
                tag.appendChild(icon);
            }
            tag.appendChild(document.createTextNode(status.replace(/_/g, " ")));
            bubble.appendChild(tag);
        }

        const body = document.createElement("span");
        body.textContent = text;
        bubble.appendChild(body);

        row.appendChild(bubble);
        chatLog.appendChild(row);
        chatLog.scrollTop = chatLog.scrollHeight;
    }

    function showTyping() {
        const row = document.createElement("div");
        row.className = "chat-row assistant";
        row.id = "typingRow";

        const avatar = document.createElement("div");
        avatar.className = "avatar";
        avatar.setAttribute("aria-hidden", "true");
        avatar.textContent = "AI";

        const bubble = document.createElement("div");
        bubble.className = "chat-bubble assistant typing-bubble";
        bubble.innerHTML = '<span class="typing-dots"><span></span><span></span><span></span></span>';

        row.appendChild(avatar);
        row.appendChild(bubble);
        chatLog.appendChild(row);
        chatLog.scrollTop = chatLog.scrollHeight;
    }

    function hideTyping() {
        document.getElementById("typingRow")?.remove();
    }

    function setConfirm(task) {
        pendingDeleteId = task ? task.id : null;
        confirmBar.classList.toggle("d-none", !task);
        if (task) {
            confirmText.textContent = `Delete task ${task.id} ("${task.title}")?`;
        }
    }

    function activateChat() {
        welcomePanel.classList.add("hidden");
        chatLog.classList.remove("hidden");
    }

    // Sidebar audit trail: every AI-proposed call this session, accepted or rejected - mirrors
    // what AssistantOrchestrator logs server-side (ILogger), but visible without tailing logs.
    function logAudit(action, status, source) {
        const empty = auditLog.querySelector(".audit-empty");
        if (empty) empty.remove();

        const severity = status === "success" ? "success"
            : (status === "ambiguous" || status === "confirmation_required") ? "warn"
            : "error";

        const entry = document.createElement("div");
        entry.className = "audit-entry audit-" + severity;

        const body = document.createElement("div");
        body.className = "audit-body";

        const actionEl = document.createElement("div");
        actionEl.className = "audit-action";
        actionEl.textContent = action || "reject_request";
        body.appendChild(actionEl);

        const meta = document.createElement("div");
        meta.className = "audit-meta";
        const time = document.createElement("span");
        time.textContent = new Date().toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit", second: "2-digit" });
        const statusSpan = document.createElement("span");
        statusSpan.textContent = source ? `${status} · ${source}` : status;
        meta.appendChild(time);
        meta.appendChild(statusSpan);
        body.appendChild(meta);

        entry.appendChild(body);
        auditLog.prepend(entry);

        while (auditLog.children.length > MAX_AUDIT_ENTRIES) {
            auditLog.removeChild(auditLog.lastElementChild);
        }
    }

    async function postJson(url, body) {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
        });
        let data = null;
        try {
            data = await res.json();
        } catch {
            /* no body */
        }
        return { ok: res.ok, status: res.status, data };
    }

    async function sendMessage(message) {
        activateChat();
        addBubble("user", message);
        setConfirm(null);
        showTyping();

        const { ok, data } = await postJson("/api/assistant", { message, conversationId });
        hideTyping();

        if (!data) {
            addBubble("assistant", "The assistant did not return a response. Please try again.", "error");
            logAudit(null, "error");
            return;
        }

        addBubble("assistant", data.result || (ok ? "Done." : "Something went wrong."), data.status);
        logAudit(data.action, data.status);

        if (data.status === "confirmation_required" && data.task) {
            setConfirm(data.task);
        }

        if (data.status === "success" || data.status === "confirmation_required") {
            loadTasks();
        }
    }

    async function confirmDelete(confirmed) {
        if (pendingDeleteId === null) return;
        const id = pendingDeleteId;
        setConfirm(null);

        if (!confirmed) {
            addBubble("assistant", "Cancelled - the task was not deleted.", "rejected");
            logAudit("delete_task", "rejected", "user cancelled");
            return;
        }

        showTyping();
        const { data } = await postJson("/api/assistant", { message: "", confirmDeleteId: id });
        hideTyping();
        addBubble("assistant", data?.result || "Done.", data?.status);
        logAudit(data?.action || "delete_task", data?.status || "success", "user confirmed");
        loadTasks();
    }

    function formatDate(value) {
        if (!value) return null;
        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return value;
        return d.toLocaleDateString(undefined, { year: "numeric", month: "short", day: "numeric" });
    }

    function renderTaskTable() {
        taskCount.textContent = allTasks.length;
        taskTableBody.innerHTML = "";

        const totalPages = Math.max(1, Math.ceil(allTasks.length / PAGE_SIZE));
        currentPage = Math.min(Math.max(1, currentPage), totalPages);

        if (allTasks.length === 0) {
            const row = document.createElement("tr");
            const cell = document.createElement("td");
            cell.colSpan = 4;
            cell.className = "empty-state";
            cell.textContent = "No tasks yet. Ask the assistant to add one.";
            row.appendChild(cell);
            taskTableBody.appendChild(row);
        } else {
            const start = (currentPage - 1) * PAGE_SIZE;
            const pageItems = allTasks.slice(start, start + PAGE_SIZE);

            for (const task of pageItems) {
                const row = document.createElement("tr");
                row.className = task.isDone ? "done" : "";

                const doneCell = document.createElement("td");
                doneCell.className = "col-done";
                const checkbox = document.createElement("input");
                checkbox.type = "checkbox";
                checkbox.className = "form-check-input";
                checkbox.checked = task.isDone;
                checkbox.addEventListener("change", () => toggleDone(task.id, checkbox.checked));
                doneCell.appendChild(checkbox);

                const idCell = document.createElement("td");
                idCell.className = "col-id";
                idCell.textContent = "#" + task.id;

                const titleCell = document.createElement("td");
                titleCell.className = "task-title-cell";
                titleCell.textContent = task.title;

                const dueCell = document.createElement("td");
                dueCell.className = "col-due";
                const due = formatDate(task.dueDate);
                dueCell.textContent = due || "—";

                row.appendChild(doneCell);
                row.appendChild(idCell);
                row.appendChild(titleCell);
                row.appendChild(dueCell);
                taskTableBody.appendChild(row);
            }
        }

        pageLabel.textContent = `Page ${currentPage} of ${totalPages}`;
        prevPageBtn.disabled = currentPage <= 1;
        nextPageBtn.disabled = currentPage >= totalPages;
    }

    async function toggleDone(id, isDone) {
        await fetch(`/api/tasks/${id}`, {
            method: "PUT",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ isDone }),
        });
        loadTasks();
    }

    async function loadTasks() {
        try {
            const res = await fetch("/api/tasks");
            if (!res.ok) return;
            allTasks = await res.json();
            renderTaskTable();
        } catch {
            /* network hiccup - leave the current table as-is */
        }
    }

    chatForm.addEventListener("submit", (e) => {
        e.preventDefault();
        const message = chatInput.value.trim();
        if (!message) return;
        chatInput.value = "";
        sendMessage(message);
    });

    document.querySelectorAll(".example-chip").forEach((chip) => {
        chip.addEventListener("click", () => {
            chatInput.value = chip.textContent;
            chatInput.focus();
        });
    });

    document.querySelectorAll(".suggestion-card").forEach((card) => {
        card.addEventListener("click", () => {
            chatInput.value = card.dataset.prompt || "";
            chatInput.focus();
        });
    });

    newChatBtn.addEventListener("click", () => {
        chatLog.innerHTML = "";
        chatLog.classList.add("hidden");
        welcomePanel.classList.remove("hidden");
        setConfirm(null);
        conversationId = newConversationId();
        chatInput.value = "";
        chatInput.focus();
    });

    refreshBtn.addEventListener("click", loadTasks);
    confirmYes.addEventListener("click", () => confirmDelete(true));
    confirmNo.addEventListener("click", () => confirmDelete(false));

    prevPageBtn.addEventListener("click", () => {
        if (currentPage > 1) {
            currentPage--;
            renderTaskTable();
        }
    });

    nextPageBtn.addEventListener("click", () => {
        const totalPages = Math.max(1, Math.ceil(allTasks.length / PAGE_SIZE));
        if (currentPage < totalPages) {
            currentPage++;
            renderTaskTable();
        }
    });

    chatLog.classList.add("hidden");
    loadTasks();
})();
