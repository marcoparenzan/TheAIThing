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
        }, 500);
    }

    addMessage(content, sender) {
        const row = document.createElement('div');
        row.className = `chat-row ${sender}`;

        const bubble = document.createElement('div');
        bubble.className = `chat-bubble ${sender}`;
        bubble.textContent = content;

        row.appendChild(bubble);
        this.chatContainer.appendChild(row);

        // Scroll to bottom
        const messagesContainer = this.chatContainer.closest('.chat-messages');
        if (messagesContainer) {
            messagesContainer.scrollTop = messagesContainer.scrollHeight;
        }
    }

    addAction(title, type) {
        const actionDiv = document.createElement('div');
        actionDiv.className = 'action-item';

        const iconHtml = `
            <div class="action-icon">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor">
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                          d="M12 10v6m0 0l-3-3m3 3l3-3M5 12h14"></path>
                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2"
                          d="M12 10v6m0 0l-3-3m3 3l3-3m2 8H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z"></path>
                </svg>
            </div>
        `;

        const titleSpan = document.createElement('span');
        titleSpan.className = 'action-title';
        titleSpan.textContent = title;

        actionDiv.innerHTML = iconHtml;
        actionDiv.appendChild(titleSpan);

        actionDiv.addEventListener('click', () => {
            alert(`Action: ${title}`);
        });

        this.actionsList.appendChild(actionDiv);
    }
}
