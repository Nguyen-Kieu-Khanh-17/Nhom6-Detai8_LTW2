const request = async (url, options = {}) => {
  const response = await fetch(url, options);
  const data = await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.message || 'Không thể thực hiện yêu cầu.');
  return data;
};

let currentUser = null;
let allTickets = [];
let allRooms = [];
let currentRoomId = null;
let currentActivePatient = null;
let chatHubConnection = null;
let unreadChatCount = 0;
let doctorsList = [];

window.switchTab = (tabName) => {
  document.querySelector('#sec-tickets').style.display = tabName === 'tickets' ? 'grid' : 'none';
  document.querySelector('#sec-livechat').style.display = tabName === 'livechat' ? 'grid' : 'none';

  document.querySelector('#nav-tickets').classList.toggle('active', tabName === 'tickets');
  document.querySelector('#nav-livechat').classList.toggle('active', tabName === 'livechat');

  const titleEl = document.querySelector('#support-title');
  if (titleEl) {
    titleEl.textContent = tabName === 'tickets' ? 'Hộp thư Chăm sóc khách hàng' : 'Phòng Chat Trực Tuyến 1-1';
  }

  if (tabName === 'livechat') {
    unreadChatCount = 0;
    updateUnreadBadge();
    loadChatRooms();
  }
};

function updateUnreadBadge() {
  const badge = document.querySelector('#chat-unread-badge');
  if (!badge) return;
  if (unreadChatCount > 0) {
    badge.textContent = `+${unreadChatCount}`;
    badge.style.display = 'inline-block';
  } else {
    badge.style.display = 'none';
  }
}

async function load() {
  currentUser = await request('/api/auth/me');
  document.querySelector('#welcome').textContent = `Xin chào ${currentUser.fullName}. Bộ phận Hỗ trợ & CSKH.`;

  await Promise.all([loadTickets(), loadDoctors()]);
  initSignalRChat();
}

async function loadDoctors() {
  doctorsList = await request('/api/doctors').catch(() => []);
}

/* --- QUẢN LÝ TICKETS --- */
async function loadTickets() {
  try {
    allTickets = await request('/api/support');
    renderTickets();
  } catch (err) {
    document.querySelector('#messages').innerHTML = '<p class="label">Không thể tải danh sách yêu cầu.</p>';
  }
}

function renderTickets() {
  const container = document.querySelector('#messages');
  const filter = document.querySelector('#ticket-filter')?.value;

  const list = filter ? allTickets.filter(t => t.status === filter) : allTickets;

  if (!list || !list.length) {
    container.innerHTML = '<p class="label">Hiện tại không có yêu cầu hỗ trợ nào.</p>';
    return;
  }

  container.innerHTML = list.map(item => {
    let statusBadge = '<span class="tag pending">Mới gửi (New)</span>';
    if (item.status === 'Processing') statusBadge = '<span class="tag confirmed">Đang xử lý</span>';
    if (item.status === 'Resolved') statusBadge = '<span class="tag" style="background:#dcfce7;color:#15803d;">Đã giải quyết</span>';

    return `
      <div class="ticket-card">
        <div class="ticket-header">
          <div>
            <strong style="font-size:1.05rem;color:#1e293b;">${escapeHtml(item.subject)}</strong>
            <small style="color:#64748b;display:block;margin-top:2px;">Bệnh nhân ID #${item.userId} · Gửi lúc: ${new Date(item.createdAt).toLocaleString('vi-VN')}</small>
          </div>
          <div>${statusBadge}</div>
        </div>
        <p style="background:#f8fafc;padding:10px 12px;border-radius:6px;font-size:0.92rem;color:#334155;margin:8px 0;">
          ${escapeHtml(item.content)}
        </p>
        ${item.reply ? `
          <div style="background:#eff6ff;border-left:3px solid #3b82f6;padding:8px 12px;border-radius:4px;font-size:0.88rem;color:#1e40af;margin-bottom:10px;">
            <strong>Phản hồi của CSKH:</strong> ${escapeHtml(item.reply)}
          </div>
        ` : ''}
        <div style="display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:8px;">
          <form onsubmit="replyTicket(event, ${item.id})" style="display:flex;gap:8px;flex:1;max-width:600px;">
            <input name="reply" placeholder="Nhập câu trả lời phản hồi..." required style="flex:1;padding:8px 12px;border:1px solid #cbd5e1;border-radius:6px;font-size:0.88rem;">
            <button class="button" style="padding:8px 14px;background:#059669;font-size:0.88rem;">Gửi trả lời</button>
          </form>
          <button class="button" style="background:#4f46e5;padding:8px 14px;font-size:0.88rem;" onclick="startLiveChatWithPatient(${item.userId})">
            💬 Chat Realtime với Bệnh nhân
          </button>
        </div>
      </div>
    `;
  }).join('');
}

document.querySelector('#ticket-filter')?.addEventListener('change', renderTickets);

window.replyTicket = async (event, id) => {
  event.preventDefault();
  const reply = event.target.reply.value;
  try {
    await request(`/api/support/${id}/reply`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ reply, status: 'Resolved' })
    });
    await loadTickets();
  } catch (error) {
    document.querySelector('#message').textContent = error.message;
  }
};

/* --- LIVE CHAT REALTIME --- */
async function loadChatRooms() {
  const container = document.querySelector('#chat-rooms-list');
  try {
    allRooms = await request('/api/chat/rooms');
    if (!allRooms || !allRooms.length) {
      container.innerHTML = '<div style="padding:14px;color:#64748b;">Chưa có phiên chat nào từ bệnh nhân.</div>';
      return;
    }

    container.innerHTML = allRooms.map(r => `
      <div class="room-item ${currentRoomId === r.id ? 'active' : ''}" onclick="selectChatRoom(${r.id}, '${escapeJs(r.patientName)}', ${r.patientUserId})">
        <strong>${escapeHtml(r.patientName)}</strong>
        <small>${escapeHtml(r.lastMessage || 'Chưa có tin nhắn')}</small>
        <small style="display:block;margin-top:2px;color:#94a3b8;">${new Date(r.updatedAt || r.createdAt).toLocaleTimeString('vi-VN')}</small>
      </div>
    `).join('');
  } catch (err) {
    container.innerHTML = '<div style="padding:14px;color:#e11d48;">Không thể tải danh sách phòng chat.</div>';
  }
}

window.selectChatRoom = async (roomId, patientName, patientUserId) => {
  currentRoomId = roomId;
  currentActivePatient = { id: patientUserId, name: patientName };

  document.querySelectorAll('.room-item').forEach(el => el.classList.remove('active'));

  document.querySelector('#active-chat-title').textContent = `💬 Đang trò chuyện với: ${patientName}`;
  document.querySelector('#active-chat-subtitle').textContent = `Phòng chat ID #${roomId} · Đang kết nối trực tuyến`;
  
  // Hiển thị các nút thao tác
  document.querySelector('#close-room-btn').style.display = 'block';
  document.querySelector('#book-for-patient-btn').style.display = 'block';

  document.querySelector('#chat-input-text').disabled = false;
  document.querySelector('#chat-send-submit').disabled = false;
  document.querySelector('#chat-input-text').focus();

  // Tham gia phòng SignalR
  if (chatHubConnection && chatHubConnection.state === signalR.HubConnectionState.Connected) {
    await chatHubConnection.invoke('JoinRoom', `chat_room_${roomId}`).catch(() => {});
  }

  await loadMessages(roomId);
};

window.startLiveChatWithPatient = async (patientUserId) => {
  try {
    const rooms = await request('/api/chat/rooms');
    let matched = rooms.find(r => r.patientUserId === patientUserId && r.status !== 'Closed');
    if (!matched) {
      matched = await request('/api/chat/my-room');
    }
    switchTab('livechat');
    await selectChatRoom(matched.id, matched.patientName, matched.patientUserId);
  } catch (e) {
    switchTab('livechat');
  }
};

async function loadMessages(roomId) {
  const box = document.querySelector('#chat-history-box');
  try {
    const msgs = await request(`/api/chat/rooms/${roomId}/messages`);
    if (!msgs || !msgs.length) {
      box.innerHTML = '<div style="text-align:center;color:#94a3b8;margin-top:40px;">Bắt đầu gửi tin nhắn để chào hỏi bệnh nhân.</div>';
      return;
    }

    box.innerHTML = msgs.map(m => {
      const isSupport = m.isFromSupport;
      return `
        <div class="bubble ${isSupport ? 'support' : 'patient'}">
          <small style="display:block;font-size:0.75rem;opacity:0.8;margin-bottom:2px;">
            ${escapeHtml(m.senderName)} · ${new Date(m.sentAt).toLocaleTimeString('vi-VN')}
          </small>
          ${escapeHtml(m.content)}
        </div>
      `;
    }).join('');

    box.scrollTop = box.scrollHeight;
  } catch (err) {
    box.innerHTML = '<div style="color:#e11d48;padding:10px;">Lỗi tải tin nhắn.</div>';
  }
}

document.querySelector('#chat-send-form')?.addEventListener('submit', async e => {
  e.preventDefault();
  const input = document.querySelector('#chat-input-text');
  const content = input.value.trim();
  if (!content || !currentRoomId) return;

  input.value = '';
  try {
    await request('/api/chat/messages', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ roomId: currentRoomId, content })
    });
    await loadMessages(currentRoomId);
  } catch (err) {
    alert(err.message);
  }
});

document.querySelector('#close-room-btn')?.addEventListener('click', async () => {
  if (!currentRoomId || !confirm('Bạn có chắc muốn đóng phiên chat này?')) return;
  try {
    await request(`/api/chat/rooms/${currentRoomId}/close`, { method: 'POST' });
    currentRoomId = null;
    currentActivePatient = null;
    document.querySelector('#active-chat-title').textContent = 'Chọn phòng để bắt đầu trò chuyện';
    document.querySelector('#active-chat-subtitle').textContent = '';
    document.querySelector('#close-room-btn').style.display = 'none';
    document.querySelector('#book-for-patient-btn').style.display = 'none';
    document.querySelector('#chat-input-text').disabled = true;
    document.querySelector('#chat-send-submit').disabled = true;
    document.querySelector('#chat-history-box').innerHTML = '<div style="text-align:center;color:#94a3b8;margin-top:60px;">Phiên chat đã kết thúc.</div>';
    await loadChatRooms();
  } catch (err) {
    alert(err.message);
  }
});

/* --- TÍNH NĂNG ĐẶT LỊCH HỘ BỆNH NHÂN TRỰC TIẾP TỪ CHAT (TASK 3) --- */
const bookModal = document.querySelector('#support-booking-modal');
const bookForm = document.querySelector('#support-booking-form');

document.querySelector('#book-for-patient-btn')?.addEventListener('click', () => {
  if (!currentActivePatient) return;

  document.querySelector('#support-book-patient-name').value = `${currentActivePatient.name} (Mã user #${currentActivePatient.id})`;
  
  // Dropdown Bác sĩ
  const docSelect = document.querySelector('#support-book-doctor');
  docSelect.innerHTML = '<option value="">-- Chọn bác sĩ chuyên khoa --</option>' +
    doctorsList.map(d => `<option value="${d.id}">${escapeHtml(d.fullName)} (Khoa ${escapeHtml(d.specialty)} - ${escapeHtml(d.room)})</option>`).join('');

  // Default datetime = ngày mai 09:00
  const tomorrow = new Date(Date.now() + 24 * 3600000);
  tomorrow.setHours(9, 0, 0, 0);
  tomorrow.setMinutes(tomorrow.getMinutes() - tomorrow.getTimezoneOffset());
  document.querySelector('#support-book-datetime').value = tomorrow.toISOString().slice(0, 16);
  document.querySelector('#support-book-reason').value = 'Đặt lịch theo yêu cầu hỗ trợ trực tuyến qua Live Chat';
  document.querySelector('#support-book-msg').textContent = '';

  bookModal.style.display = 'flex';
});

window.closeSupportBookingModal = () => {
  bookModal.style.display = 'none';
};

bookForm?.addEventListener('submit', async (e) => {
  e.preventDefault();
  const msgEl = document.querySelector('#support-book-msg');
  const doctorId = Number(document.querySelector('#support-book-doctor').value);
  const apptDate = document.querySelector('#support-book-datetime').value;
  const reason = document.querySelector('#support-book-reason').value.trim();

  try {
    msgEl.style.color = '#0284c7';
    msgEl.textContent = 'Đang tạo lịch hẹn hộ bệnh nhân...';

    const newAppt = await request('/api/appointments', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        patientId: 0,
        userId: currentActivePatient.id,
        doctorId: doctorId,
        appointmentDate: apptDate,
        reason: reason,
        status: 'Pending'
      })
    });

    const docObj = doctorsList.find(d => d.id === doctorId);
    const docName = docObj ? docObj.fullName : `Bác sĩ #${doctorId}`;

    // Tự động gửi tin nhắn xác nhận vào khung chat cho bệnh nhân
    const confirmationText = `✓ Tôi đã tạo lịch hẹn khám giúp bạn thành công!\n• Bác sĩ: ${docName}\n• Thời gian: ${new Date(apptDate).toLocaleString('vi-VN')}\n• Mã lịch hẹn: #${newAppt.id}\nLễ tân sẽ liên hệ duyệt lịch. Bạn hãy vào mục 'Lịch hẹn của tôi' để theo dõi nhé!`;

    await request('/api/chat/messages', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ roomId: currentRoomId, content: confirmationText })
    });

    msgEl.style.color = '#059669';
    msgEl.textContent = '✓ Đã đặt lịch hộ và thông báo tới Bệnh nhân thành công!';

    setTimeout(() => {
      closeSupportBookingModal();
      loadMessages(currentRoomId);
    }, 1200);

  } catch (err) {
    msgEl.style.color = '#dc2626';
    msgEl.textContent = err.message;
  }
});

/* --- SIGNALR REALTIME --- */
function initSignalRChat() {
  if (typeof signalR === 'undefined') return;
  chatHubConnection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/clinic')
    .withAutomaticReconnect()
    .build();

  chatHubConnection.on('ReceiveChatMessage', (msg) => {
    // Nếu tin nhắn đến phòng đang mở
    if (currentRoomId && msg.roomId === currentRoomId) {
      const box = document.querySelector('#chat-history-box');
      const isSupport = msg.isFromSupport;
      const bubble = document.createElement('div');
      bubble.className = `bubble ${isSupport ? 'support' : 'patient'}`;
      bubble.innerHTML = `
        <small style="display:block;font-size:0.75rem;opacity:0.8;margin-bottom:2px;">
          ${escapeHtml(msg.senderName)} · ${new Date(msg.sentAt).toLocaleTimeString('vi-VN')}
        </small>
        ${escapeHtml(msg.content)}
      `;
      box.appendChild(bubble);
      box.scrollTop = box.scrollHeight;
    } else {
      // Tin nhắn mới từ bệnh nhân khác
      if (!msg.isFromSupport) {
        unreadChatCount++;
        updateUnreadBadge();
        // Beep sound
        playNotificationBeep();
      }
    }
    loadChatRooms();
  });

  chatHubConnection.on('NewChatRoomCreated', () => {
    unreadChatCount++;
    updateUnreadBadge();
    playNotificationBeep();
    loadChatRooms();
  });

  chatHubConnection.start().catch(err => console.log('SignalR reconnecting...', err));
}

function playNotificationBeep() {
  try {
    const ctx = new (window.AudioContext || window.webkitAudioContext)();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    osc.connect(gain);
    gain.connect(ctx.destination);
    osc.frequency.setValueAtTime(800, ctx.currentTime);
    gain.gain.setValueAtTime(0.2, ctx.currentTime);
    osc.start();
    osc.stop(ctx.currentTime + 0.15);
  } catch (e) { /* ignore */ }
}

document.querySelector('#logout')?.addEventListener('click', async () => {
  await request('/api/auth/logout', { method: 'POST' });
  location.href = '/login.html';
});

function escapeHtml(str) {
  return String(str || '').replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[c]);
}

function escapeJs(str) {
  return String(str || '').replace(/'/g, "\\'");
}

load().catch(() => { location.href = '/login.html'; });
