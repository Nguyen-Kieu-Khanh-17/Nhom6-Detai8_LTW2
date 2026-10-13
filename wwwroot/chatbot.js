(function () {
  // Tạo DOM cho Chatbot Widget
  const chatbotHtml = `
    <button id="chatbot-toggle-btn" class="chatbot-toggle" title="Mở Trợ lý AI Medika">
      <span class="bot-avatar">🤖</span>
      <span>Trợ lý Medika AI</span>
    </button>

    <div id="chatbot-window" class="chatbot-window hidden">
      <div class="chatbot-header">
        <div class="chatbot-header-info">
          <span style="font-size:1.4rem;">🤖</span>
          <div>
            <h4>Medika AI Smart Assistant</h4>
            <small id="chatbot-status-subtitle"><span class="chatbot-status-dot"></span> Trực tuyến 24/7 · Gemini AI Enabled</small>
          </div>
        </div>
        <div style="display:flex;align-items:center;gap:6px;">
          <button id="chatbot-clear-btn" class="chatbot-close" title="Làm mới cuộc trò chuyện" style="font-size:0.9rem;">↺</button>
          <button id="chatbot-close-btn" class="chatbot-close" title="Đóng chat">✕</button>
        </div>
      </div>

      <div id="chatbot-messages" class="chatbot-messages">
        <div class="chat-msg bot">
          Xin chào! Tôi là <strong>Trợ lý Y tế Thông minh của Medika Clinic</strong> 🏥.<br><br>
          Tôi có thể hỗ trợ bạn tìm chuyên khoa, tra cứu bác sĩ, thủ tục BHYT và hướng dẫn đặt lịch khám nhanh chóng. Bạn cần hỗ trợ gì hôm nay?
        </div>
      </div>

      <div id="chatbot-prompts" class="chatbot-prompts"></div>

      <div class="chatbot-input-bar">
        <input type="text" id="chatbot-input" placeholder="Nhập câu hỏi, triệu chứng cần tư vấn..." autocomplete="off">
        <button id="chatbot-send-btn" class="chatbot-send-btn" title="Gửi">➤</button>
      </div>
    </div>
  `;

  // Nhúng vào body khi trang tải xong
  document.addEventListener('DOMContentLoaded', initChatbot);
  if (document.readyState === 'interactive' || document.readyState === 'complete') {
    initChatbot();
  }

  function initChatbot() {
    if (document.querySelector('#chatbot-toggle-btn')) return;

    const wrapper = document.createElement('div');
    wrapper.id = 'chatbot-container';
    wrapper.innerHTML = chatbotHtml;
    document.body.appendChild(wrapper);

    const toggleBtn = document.querySelector('#chatbot-toggle-btn');
    const windowEl = document.querySelector('#chatbot-window');
    const closeBtn = document.querySelector('#chatbot-close-btn');
    const clearBtn = document.querySelector('#chatbot-clear-btn');
    const inputEl = document.querySelector('#chatbot-input');
    const sendBtn = document.querySelector('#chatbot-send-btn');
    const messagesEl = document.querySelector('#chatbot-messages');
    const promptsEl = document.querySelector('#chatbot-prompts');

    toggleBtn.addEventListener('click', () => {
      windowEl.classList.toggle('hidden');
      if (!windowEl.classList.contains('hidden')) {
        inputEl.focus();
      }
    });

    closeBtn.addEventListener('click', () => {
      windowEl.classList.add('hidden');
    });

    clearBtn?.addEventListener('click', () => {
      messagesEl.innerHTML = `
        <div class="chat-msg bot">
          Xin chào! Tôi là <strong>Trợ lý Y tế Thông minh của Medika Clinic</strong> 🏥.<br><br>
          Tôi có thể hỗ trợ bạn tư vấn chuyên khoa, tra cứu bác sĩ, chính sách BHYT và đặt lịch khám. Bạn cần giúp gì ạ?
        </div>
      `;
      loadPrompts();
    });

    sendBtn.addEventListener('click', () => sendMessage());
    inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') sendMessage();
    });

    loadPrompts();

    async function loadPrompts() {
      try {
        const res = await fetch('/api/chatbot/prompts');
        if (!res.ok) return;
        const data = await res.json();
        promptsEl.innerHTML = (data || []).map(p => `
          <button class="chatbot-prompt-chip" data-action="${escapeHtml(p.actionType)}" data-value="${escapeHtml(p.value || '')}">${escapeHtml(p.label)}</button>
        `).join('');

        promptsEl.querySelectorAll('.chatbot-prompt-chip').forEach(btn => {
          btn.addEventListener('click', () => {
            const action = btn.getAttribute('data-action');
            const val = btn.getAttribute('data-value');
            handleQuickAction(action, val);
          });
        });
      } catch (e) { /* ignore */ }
    }

    async function sendMessage(text) {
      const msg = (typeof text === 'string' ? text : inputEl.value).trim();
      if (!msg) return;

      appendMsg(msg, 'user');
      inputEl.value = '';

      // Hiệu ứng đang phân tích
      const typingEl = appendMsg('Đang phân tích dữ liệu y khoa...', 'bot');

      try {
        const res = await fetch('/api/chatbot/message', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ message: msg })
        });

        const data = await res.json();
        typingEl.remove();

        if (!res.ok) {
          appendMsg(data?.message || 'Xin lỗi, tôi gặp sự cố khi xử lý.', 'bot');
          return;
        }

        // Định dạng Markdown
        let formatted = formatMarkdown(data.reply || '');

        let botReplyHtml = `<div>${formatted}</div>`;

        // Nguồn AI
        if (data.source) {
          const isGemini = data.source.toLowerCase().includes('gemini');
          const badgeStyle = isGemini
            ? 'background:#e0e7ff;color:#3730a3;border:1px solid #c7d2fe;'
            : 'background:#f1f5f9;color:#475569;border:1px solid #e2e8f0;';
          botReplyHtml += `
            <div style="margin-top:6px;display:flex;align-items:center;gap:4px;">
              <span style="font-size:0.72rem;padding:2px 8px;border-radius:10px;font-weight:600;${badgeStyle}">
                ${isGemini ? '⚡ ' : '🏥 '}${escapeHtml(data.source)}
              </span>
            </div>
          `;
        }

        // Nếu có danh sách bác sĩ
        if (data.doctors && data.doctors.length > 0) {
          botReplyHtml += `<div class="chatbot-doctors-list">` +
            data.doctors.map(d => `
              <div class="chatbot-doc-card">
                <div class="chatbot-doc-info">
                  <strong>${escapeHtml(d.fullName)}</strong>
                  <small>${escapeHtml(d.specialty)} · ${escapeHtml(d.room)}</small>
                </div>
                <button class="chatbot-doc-btn" onclick="window.bookDoctorDirectly(${d.id}, '${escapeHtml(d.specialty)}')">Đặt lịch</button>
              </div>
            `).join('') +
            `</div>`;
        }

        // Hiển thị quick actions nếu có
        if (data.quickActions && data.quickActions.length > 0) {
          botReplyHtml += `<div style="display:flex;gap:6px;flex-wrap:wrap;margin-top:10px;">` +
            data.quickActions.map(qa => `
              <button class="chatbot-prompt-chip" onclick="window.handleChatbotAction('${qa.actionType}', '${escapeHtml(qa.value || '')}')">${escapeHtml(qa.label)}</button>
            `).join('') +
            `</div>`;
        }

        const msgDiv = document.createElement('div');
        msgDiv.className = 'chat-msg bot';
        msgDiv.innerHTML = botReplyHtml;
        messagesEl.appendChild(msgDiv);
        messagesEl.scrollTop = messagesEl.scrollHeight;

      } catch (err) {
        typingEl.remove();
        appendMsg('Không thể kết nối đến máy chủ trợ lý AI.', 'bot');
      }
    }

    function appendMsg(text, sender) {
      const el = document.createElement('div');
      el.className = `chat-msg ${sender}`;
      el.textContent = text;
      messagesEl.appendChild(el);
      messagesEl.scrollTop = messagesEl.scrollHeight;
      return el;
    }

    function handleQuickAction(action, val) {
      if (action === 'info' && val) {
        sendMessage(val);
      } else if (action === 'book') {
        window.bookDoctorDirectly(null, val || '');
      } else if (action === 'support') {
        location.href = '/support.html';
      } else if (action === 'specialty') {
        sendMessage(`Danh sách các bác sĩ chuyên khoa ${val || 'tổng quát'}`);
      }
    }
    window.handleChatbotAction = handleQuickAction;

    window.bookDoctorDirectly = (doctorId, specialty) => {
      if (location.pathname.includes('/user.html')) {
        const specSelect = document.querySelector('#specialty');
        const docSelect = document.querySelector('#doctor-id');
        const formEl = document.querySelector('#booking-form');

        if (specSelect && specialty) {
          for (let opt of specSelect.options) {
            if (opt.value.toLowerCase().includes(specialty.toLowerCase())) {
              specSelect.value = opt.value;
              specSelect.dispatchEvent(new Event('change'));
              break;
            }
          }
        }
        if (docSelect && doctorId) {
          setTimeout(() => {
            docSelect.value = doctorId;
          }, 300);
        }
        if (formEl) {
          formEl.scrollIntoView({ behavior: 'smooth' });
          windowEl.classList.add('hidden');
        }
      } else {
        location.href = `/user.html?specialty=${encodeURIComponent(specialty || '')}&doctorId=${doctorId || ''}`;
      }
    };

    function formatMarkdown(text) {
      let escaped = escapeHtml(text);
      // In đậm
      escaped = escaped.replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>');
      // In nghiêng
      escaped = escaped.replace(/\*(.*?)\*/g, '<em>$1</em>');
      // Xuống dòng
      escaped = escaped.replace(/\n/g, '<br>');
      return escaped;
    }

    function escapeHtml(str) {
      return String(str || '').replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[c]);
    }
  }
})();
