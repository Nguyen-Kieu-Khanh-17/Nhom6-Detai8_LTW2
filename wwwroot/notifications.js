// notifications.js - Hệ thống thông báo Realtime và Chuông thông báo Medika
(function () {
  let connection = null;
  let unreadCount = 0;
  let notificationsList = [];

  // Tạo UI Chuông thông báo và Panel
  function injectNotificationUI() {
    const container = document.createElement('div');
    container.id = 'medika-notification-hub';
    container.innerHTML = `
      <style>
        #medika-notification-hub {
          position: fixed;
          top: 14px;
          right: 70px;
          z-index: 9999;
          font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
        }
        .noti-bell-btn {
          background: #ffffff;
          border: 1px solid #e2e8f0;
          border-radius: 50%;
          width: 42px;
          height: 42px;
          display: flex;
          align-items: center;
          justify-content: center;
          cursor: pointer;
          position: relative;
          box-shadow: 0 2px 8px rgba(0,0,0,0.08);
          font-size: 1.25rem;
          transition: all 0.2s ease;
        }
        .noti-bell-btn:hover {
          background: #f8fafc;
          transform: scale(1.05);
        }
        .noti-badge {
          position: absolute;
          top: -4px;
          right: -4px;
          background: #ef4444;
          color: #ffffff;
          font-size: 0.72rem;
          font-weight: 700;
          padding: 2px 6px;
          border-radius: 999px;
          min-width: 18px;
          text-align: center;
          display: none;
          box-shadow: 0 1px 3px rgba(0,0,0,0.2);
        }
        .noti-badge.show {
          display: inline-block;
          animation: noti-pulse 0.4s ease;
        }
        @keyframes noti-pulse {
          0% { transform: scale(0.6); }
          50% { transform: scale(1.2); }
          100% { transform: scale(1); }
        }
        .noti-panel {
          position: absolute;
          top: 50px;
          right: 0;
          width: 360px;
          max-width: 90vw;
          max-height: 480px;
          background: #ffffff;
          border: 1px solid #e2e8f0;
          border-radius: 12px;
          box-shadow: 0 10px 25px -5px rgba(0,0,0,0.15);
          display: none;
          flex-direction: column;
          overflow: hidden;
          animation: noti-slide-down 0.2s ease;
        }
        @keyframes noti-slide-down {
          from { opacity: 0; transform: translateY(-8px); }
          to { opacity: 1; transform: translateY(0); }
        }
        .noti-panel.open {
          display: flex;
        }
        .noti-header {
          padding: 12px 16px;
          background: #f8fafc;
          border-bottom: 1px solid #e2e8f0;
          display: flex;
          justify-content: space-between;
          align-items: center;
        }
        .noti-header h3 {
          margin: 0;
          font-size: 0.95rem;
          font-weight: 700;
          color: #1e293b;
        }
        .noti-header button {
          background: none;
          border: none;
          color: #3b82f6;
          font-size: 0.78rem;
          cursor: pointer;
          padding: 4px;
        }
        .noti-header button:hover {
          text-decoration: underline;
        }
        .noti-list {
          overflow-y: auto;
          flex: 1;
          max-height: 380px;
        }
        .noti-item {
          padding: 12px 16px;
          border-bottom: 1px solid #f1f5f9;
          cursor: pointer;
          transition: background 0.15s;
        }
        .noti-item:hover {
          background: #f8fafc;
        }
        .noti-item.unread {
          background: #eff6ff;
        }
        .noti-item strong {
          display: block;
          font-size: 0.88rem;
          color: #0f172a;
          margin-bottom: 4px;
        }
        .noti-item p {
          margin: 0;
          font-size: 0.8rem;
          color: #475569;
          line-height: 1.4;
        }
        .noti-item small {
          display: block;
          margin-top: 6px;
          font-size: 0.72rem;
          color: #94a3b8;
        }
        .noti-toast-container {
          position: fixed;
          bottom: 24px;
          right: 24px;
          z-index: 10000;
          display: flex;
          flex-direction: column;
          gap: 10px;
          pointer-events: none;
        }
        .noti-toast {
          pointer-events: auto;
          min-width: 300px;
          max-width: 400px;
          background: #1e293b;
          color: #ffffff;
          padding: 14px 18px;
          border-radius: 10px;
          box-shadow: 0 8px 20px rgba(0,0,0,0.25);
          display: flex;
          align-items: flex-start;
          gap: 12px;
          animation: noti-toast-in 0.3s ease forwards;
        }
        .noti-toast.NEW_BOOKING { border-left: 5px solid #3b82f6; }
        .noti-toast.BOOKING_SUCCESS { border-left: 5px solid #10b981; }
        .noti-toast.PATIENT_WAITING { border-left: 5px solid #8b5cf6; }
        .noti-toast.RECORD_READY { border-left: 5px solid #10b981; }
        .noti-toast.PAYMENT_PENDING { border-left: 5px solid #f59e0b; }
        .noti-toast.RESCHEDULE_REQUIRED { border-left: 5px solid #ef4444; }
        .noti-toast.LEAVE_APPROVED_COORDINATE { border-left: 5px solid #f97316; }
        .noti-toast.NEW_LEAVE_REQUEST { border-left: 5px solid #06b6d4; }
        @keyframes noti-toast-in {
          from { opacity: 0; transform: translateY(20px); }
          to { opacity: 1; transform: translateY(0); }
        }
      </style>
      <button class="noti-bell-btn" id="noti-bell-btn" title="Thông báo hệ thống">
        🔔
        <span class="noti-badge" id="noti-badge">0</span>
      </button>
      <div class="noti-panel" id="noti-panel">
        <div class="noti-header">
          <h3>Thông báo</h3>
          <button id="noti-mark-all-read">Đã đọc tất cả</button>
        </div>
        <div class="noti-list" id="noti-items">
          <p style="padding:16px;text-align:center;color:#94a3b8;font-size:0.85rem;">Không có thông báo nào.</p>
        </div>
      </div>
      <div class="noti-toast-container" id="noti-toast-container"></div>
    `;
    document.body.appendChild(container);

    // Event mở/đóng panel
    const bellBtn = document.querySelector('#noti-bell-btn');
    const panel = document.querySelector('#noti-panel');
    bellBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      panel.classList.toggle('open');
      if (panel.classList.contains('open')) {
        loadNotifications();
      }
    });

    document.addEventListener('click', (e) => {
      if (!container.contains(e.target)) {
        panel.classList.remove('open');
      }
    });

    document.querySelector('#noti-mark-all-read').addEventListener('click', async () => {
      await fetch('/api/notifications/read-all', { method: 'PATCH' });
      await loadNotifications();
      updateBadge(0);
    });
  }

  // Tải danh sách thông báo từ API DB khi F5
  async function loadNotifications() {
    try {
      const res = await fetch('/api/notifications?limit=25');
      if (!res.ok) return;
      notificationsList = await res.json();
      renderNotifications();

      const unread = notificationsList.filter(n => !n.isRead).length;
      updateBadge(unread);
    } catch (err) {
      console.warn('Không thể tải thông báo:', err);
    }
  }

  function renderNotifications() {
    const listEl = document.querySelector('#noti-items');
    if (!notificationsList.length) {
      listEl.innerHTML = '<p style="padding:16px;text-align:center;color:#94a3b8;font-size:0.85rem;">Không có thông báo nào.</p>';
      return;
    }

    listEl.innerHTML = notificationsList.map(item => `
      <div class="noti-item ${item.isRead ? '' : 'unread'}" onclick="window.markNotiRead(${item.id}, '${item.linkAction || ''}')">
        <strong>${escapeHtml(item.title)}</strong>
        <p>${escapeHtml(item.content)}</p>
        <small>${new Date(item.createdAt).toLocaleString('vi-VN')}</small>
      </div>
    `).join('');
  }

  window.markNotiRead = async function (id, linkAction) {
    try {
      await fetch(`/api/notifications/${id}/read`, { method: 'PATCH' });
      const found = notificationsList.find(n => n.id === id);
      if (found) found.isRead = true;
      renderNotifications();
      const unread = notificationsList.filter(n => !n.isRead).length;
      updateBadge(unread);

      if (linkAction && linkAction !== '#' && linkAction !== window.location.pathname) {
        window.location.href = linkAction;
      }
    } catch (e) {}
  };

  function updateBadge(count) {
    unreadCount = count;
    const badge = document.querySelector('#noti-badge');
    if (!badge) return;
    if (count > 0) {
      badge.textContent = count > 99 ? '99+' : count;
      badge.classList.add('show');
    } else {
      badge.classList.remove('show');
    }
  }

  // Hiển thị Toast nổi thông báo Realtime
  function showRealtimeToast(title, content, type = 'NEW_BOOKING') {
    const container = document.querySelector('#noti-toast-container');
    if (!container) return;

    const toast = document.createElement('div');
    toast.className = `noti-toast ${type}`;
    let icon = '🔔';
    if (type === 'NEW_BOOKING') icon = '📅';
    if (type === 'BOOKING_SUCCESS') icon = '✓';
    if (type === 'PATIENT_WAITING') icon = '🏥';
    if (type === 'RECORD_READY') icon = '💊';
    if (type === 'PAYMENT_PENDING') icon = '💳';
    if (type === 'RESCHEDULE_REQUIRED') icon = '⚠';
    if (type === 'LEAVE_APPROVED_COORDINATE') icon = '📢';
    if (type === 'NEW_LEAVE_REQUEST') icon = '📝';

    toast.innerHTML = `
      <div style="font-size:1.5rem;line-height:1;">${icon}</div>
      <div style="flex:1;">
        <strong style="display:block;font-size:0.92rem;margin-bottom:3px;color:#f8fafc;">${escapeHtml(title)}</strong>
        <p style="margin:0;font-size:0.82rem;color:#cbd5e1;line-height:1.4;">${escapeHtml(content)}</p>
      </div>
    `;

    container.appendChild(toast);
    setTimeout(() => {
      toast.style.transition = 'opacity 0.4s ease, transform 0.4s ease';
      toast.style.opacity = '0';
      toast.style.transform = 'translateY(10px)';
      setTimeout(() => toast.remove(), 400);
    }, 4500);
  }

  // Khởi tạo SignalR
  async function startSignalR() {
    if (typeof signalR === 'undefined') {
      console.warn('Chưa nạp thư viện SignalR client.');
      return;
    }

    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/clinic')
      .withAutomaticReconnect()
      .build();

    // 1. Event NEW_BOOKING (Khi Patient đặt lịch -> Lễ tân nhận)
    connection.on('NEW_BOOKING', data => {
      showRealtimeToast(data.title || 'Lịch hẹn mới', data.content || `Bệnh nhân ${data.patientName} vừa đặt lịch.`, 'NEW_BOOKING');
      updateBadge(unreadCount + 1);
      // Tự động reload danh sách lịch hẹn của Lễ tân nếu có
      if (typeof window.loadAppointments === 'function') {
        window.loadAppointments();
      }
    });

    // 2. Event BOOKING_SUCCESS (Khi Lễ tân duyệt -> Patient nhận)
    connection.on('BOOKING_SUCCESS', data => {
      showRealtimeToast(data.title || 'Lịch khám được duyệt', data.content || 'Lịch hẹn của bạn đã được xác nhận!', 'BOOKING_SUCCESS');
      updateBadge(unreadCount + 1);
      // Tự động reload lịch hẹn của Patient nếu có
      if (typeof window.loadAppointments === 'function') {
        window.loadAppointments();
      }
    });

    // 3. Event PATIENT_WAITING (Khi Lễ tân check-in -> Bác sĩ nhận)
    connection.on('PATIENT_WAITING', data => {
      showRealtimeToast(data.title || 'Bệnh nhân chờ khám', data.content || `Bệnh nhân ${data.patientName} (STT #${data.queueNumber}) đã vào hàng chờ.`, 'PATIENT_WAITING');
      updateBadge(unreadCount + 1);
      // Tự động reload hàng chờ của Bác sĩ nếu có
      if (typeof window.loadQueue === 'function') {
        window.loadQueue();
      }
    });

    // 4. Event RECORD_READY (Khi Bác sĩ khám xong -> Patient nhận)
    connection.on('RECORD_READY', data => {
      showRealtimeToast(data.title || 'Kết quả khám sẵn sàng', data.content || 'Đơn thuốc và kết quả khám đã được cập nhật!', 'RECORD_READY');
      updateBadge(unreadCount + 1);
      // Tự động reload hồ sơ khám và lịch hẹn của Patient
      if (typeof window.loadMedicalRecords === 'function') {
        window.loadMedicalRecords();
      }
      if (typeof window.loadAppointments === 'function') {
        window.loadAppointments();
      }
    });

    // 5. Event PAYMENT_PENDING (Khi Bác sĩ khám xong -> Lễ tân nhận)
    connection.on('PAYMENT_PENDING', data => {
      showRealtimeToast(data.title || 'Khám hoàn tất', data.content || `Bệnh nhân ${data.patientName} đã khám xong, chờ thanh toán.`, 'PAYMENT_PENDING');
      updateBadge(unreadCount + 1);
      if (typeof window.loadAppointments === 'function') {
        window.loadAppointments();
      }
    });

    // 6. Event RESCHEDULE_REQUIRED (Bác sĩ xin nghỉ -> Patient nhận thông báo đổi lịch)
    connection.on('RESCHEDULE_REQUIRED', data => {
      showRealtimeToast(data.title || 'Thay đổi lịch khám', data.content || 'Bác sĩ có ca đột xuất, vui lòng chọn lại thời gian hoặc bác sĩ khác.', 'RESCHEDULE_REQUIRED');
      updateBadge(unreadCount + 1);
      if (typeof window.loadAppointments === 'function') {
        window.loadAppointments();
      }
    });

    // 7. Event LEAVE_APPROVED_COORDINATE (Admin duyệt nghỉ -> Lễ tân nhận thông báo điều phối)
    connection.on('LEAVE_APPROVED_COORDINATE', data => {
      showRealtimeToast(data.title || 'Điều phối bệnh nhân', data.content || `Cần điều phối lại ${data.affectedCount} bệnh nhân của Bác sĩ ${data.doctorName}.`, 'LEAVE_APPROVED_COORDINATE');
      updateBadge(unreadCount + 1);
      if (typeof window.loadAppointments === 'function') {
        window.loadAppointments();
      }
    });

    // 8. Event NEW_LEAVE_REQUEST (Bác sĩ nộp đơn -> Admin nhận)
    connection.on('NEW_LEAVE_REQUEST', data => {
      showRealtimeToast(data.title || 'Yêu cầu xin nghỉ mới', data.content || `Bác sĩ ${data.doctorName} xin nghỉ ca ${data.shift}.`, 'NEW_LEAVE_REQUEST');
      updateBadge(unreadCount + 1);
      if (typeof window.loadLeaveRequests === 'function') {
        window.loadLeaveRequests();
      }
    });

    // Event thông báo chung
    connection.on('NOTIFICATION_RECEIVED', notification => {
      showRealtimeToast(notification.title, notification.content, notification.type || 'NEW_BOOKING');
      updateBadge(unreadCount + 1);
    });

    try {
      await connection.start();
      console.log('✓ Kết nối SignalR ClinicHub thành công.');
    } catch (err) {
      console.warn('Lỗi kết nối SignalR Hub:', err);
    }
  }

  function escapeHtml(str) {
    return String(str || '').replace(/[&<>'"]/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' })[c]);
  }

  // Khởi động khi DOM sẵn sàng
  window.addEventListener('DOMContentLoaded', async () => {
    // Không hiển thị chuông ở trang login hoặc register
    const path = window.location.pathname.toLowerCase();
    if (path.includes('login') || path.includes('register') || path.includes('forgot')) return;

    injectNotificationUI();
    await loadNotifications();
    await startSignalR();
  });
})();
