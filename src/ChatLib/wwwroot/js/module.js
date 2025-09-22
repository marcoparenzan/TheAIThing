export function setup(moduleId, proxy, canv, staticPath) {
    window.powerBiEmbeddingLib = window.powerBiEmbeddingLib || {};
    const that = window.powerBiEmbeddingLib[moduleId] || {};

    that.proxy = proxy;
    that.canvas = canv;
    that.staticPath = staticPath;

    that.component = new Chat(window.document, that.proxy, that.canvas);

    window.powerBiEmbeddingLib[moduleId] = that;
}

export function start(moduleId) {
    // Optional: hook for future initialization
    window.powerBiEmbeddingLib = window.powerBiEmbeddingLib || {};
    const that = window.powerBiEmbeddingLib[moduleId];
    if (!that || !that.component) return;
}

export function set(moduleId, name, value) {
    window.powerBiEmbeddingLib = window.powerBiEmbeddingLib || {};
    const that = window.powerBiEmbeddingLib[moduleId];
    if (!that) return;
    that[name] = value;
}

export function showReport(powerBiEmbeddingId, accessToken, embedUrl, embedReportId) {
    window.powerBiEmbeddingLib = window.powerBiEmbeddingLib || {};
    const that = window.powerBiEmbeddingLib[powerBiEmbeddingId] || {};
    that.component?.showReport(accessToken, embedUrl, embedReportId);
    window.powerBiEmbeddingLib[powerBiEmbeddingId] = that;
}

class Chat {
    constructor(doc, proxy, canv) {
        this.doc = doc;
        this.proxy = proxy;
        this.canv = canv;

        this.interface = new ChatInterface(
            this.doc.getElementById('chatContainer'),
            this.doc.getElementById('messageInput'),
            this.doc.getElementById('sendButton'),
            this.doc.getElementById('actionsList')
        );
    }

    showReport(accessToken, embedUrl, embedReportId) {
        const models = window['powerbi-client']?.models;
        if (!models) return;

        const config = {
            type: 'report',
            tokenType: models.TokenType.Embed,
            accessToken: accessToken,
            embedUrl: embedUrl,
            id: embedReportId,
            permissions: models.Permissions.All,
            settings: {
                filterPaneEnabled: true,
                navContentPaneEnabled: true
            }
        };
        window.powerbi.embed(this.canv, config);
    }
}

class ChatInterface {
    constructor(chatContainer, messageInput, sendButton, actionsList) {
        this.chatContainer = chatContainer;
        this.messageInput = messageInput;
        this.sendButton = sendButton;
        this.actionsList = actionsList;
        this.messageCounter = 0;
        // Find the scrollable viewport to auto-scroll properly
        this.scroller = this.chatContainer.closest('.chat-messages') || this.chatContainer;

        this.initEventListeners();
    }

    initEventListeners() {
        this.sendButton.addEventListener('click', () => this.sendMessage());
        this.messageInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                e.preventDefault();
                this.sendMessage();
            }
        });
    }

    sendMessage() {
        const message = this.messageInput.value.trim();
        if (!message) return;

        this.addMessage(message, 'user');
        this.messageInput.value = '';
        this.messageInput.focus();

        setTimeout(() => {
            this.addMessage('This is a simulated AI response to your message.', 'ai');
            this.addAction(`Download Response ${++this.messageCounter}`, 'download');
            this.messageInput.focus();
        }, 500);
    }

    addMessage(content, sender) {
        const row = document.createElement('div');
        row.className = `chat-row ${sender}`;

        const bubble = document.createElement('div');
        bubble.className = `chat-bubble ${sender}`;
        bubble.innerHTML = content;

        row.appendChild(bubble);
        this.chatContainer.appendChild(row);

        const scroller = this.scroller || this.chatContainer;
        scroller.scrollTop = scroller.scrollHeight;
    }

    addAction(title, type) {
        const actionDiv = document.createElement('div');
        actionDiv.className = 'action-item';

        actionDiv.innerHTML = `
            <div class="action-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" aria-hidden="true">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                          d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path>
                </svg>
            </div>
            <span class="action-title">${title}</span>
        `;

        actionDiv.addEventListener('click', () => {
            alert(`Action: ${title}`);
        });

        this.actionsList.appendChild(actionDiv);
    }
}
