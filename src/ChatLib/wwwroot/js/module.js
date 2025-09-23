let dotNetHelper = null;

export function setup(moduleId, canv, staticPath, dotNetRef) {
    
    window.chatLib = window.chatLib || {};
    const that = window.chatLib[moduleId] || {};

    that.canvas = canv;
    that.staticPath = staticPath;
    that.dotNetHelper = dotNetRef;

    that.component = new Chat(moduleId, window.document, that.dotNetRef, that.canvas);

    window.chatLib[moduleId] = that;
}

export function addMessage(moduleId, content, sender) {
    window.chatLib = window.chatLib || {};
    const that = window.chatLib[moduleId] || {};
    that.component?.addMessage(content, sender);
    window.chatLib[moduleId] = that;
}

// Example of calling the C# method from JavaScript
export function sendMessageToCSharp(moduleId, message) {
    window.chatLib = window.chatLib || {};
    const that = window.chatLib[moduleId];
    if (that.dotNetHelper) {
        that.dotNetHelper.invokeMethodAsync('OnMessageReceived', message);
    }
}

class Chat {
    constructor(moduleId, doc, proxy, canv) {
        this.moduleId = moduleId;
        this.doc = doc;
        this.proxy = proxy;
        this.canv = canv;

        this.chatContainer = this.doc.getElementById('chatContainer'),
        this.messageInput = this.doc.getElementById('messageInput'),
        this.sendButton = this.doc.getElementById('sendButton'),
        this.actionsList = this.doc.getElementById('actionsList')
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

        // Send message to C# method
        sendMessageToCSharp(this.moduleId, message);

        //setTimeout(() => {
        //    this.addMessage('This is a simulated AI response to your message.', 'ai');
        //    this.addAction(`Download Response ${++this.messageCounter}`, 'download');
        //}, 500);
    }

    addMessage(content, sender) {
        const row = document.createElement('div');
        row.className = `chat-row ${sender}`;

        const bubble = document.createElement('div');
        bubble.className = `chat-bubble ${sender}`;
        bubble.innerHTML = content;

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
