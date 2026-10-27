const doctorMessage = document.querySelector('#message');
const request = async (url, options = {}) => {
  const response = await fetch(url, options);
  const data = await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.message || 'Không thể thực hiện yêu cầu.');
  return data;
};

let currentDoctorUser = null;
let patientsList = [];
let allMedicalRecords = [];

async function loadUser() {
  currentDoctorUser = await request('/api/auth/me');
  document.querySelector('#welcome').textContent = currentDoctorUser.doctorId
    ? `Xin chào Bác sĩ ${currentDoctorUser.fullName}. Chúc bạn có một ngày làm việc hiệu quả.`
    : `Tài khoản ${currentDoctorUser.fullName} chưa được gắn hồ sơ bác sĩ. Vui lòng liên hệ quản trị viên để được phân công.`;
}

async function loadPatientsList() {
  patientsList = await request('/api/patients').catch(() => []);
}

async function loadQueue() {
  const queueContainer = document.querySelector('#patient-queue');
  try {
    const checkedInAppointments = await request('/api/appointments?status=CheckedIn');
    
    // Update metric
    const waitingEl = document.querySelector('#doc-waiting-count');
    if (waitingEl) waitingEl.textContent = checkedInAppointments?.length || 0;

    if (!checkedInAppointments || !checkedInAppointments.length) {
      queueContainer.innerHTML = '<p class="label">Hiện tại không có bệnh nhân nào trong hàng chờ.</p>';
      return;
    }

    // Sort by QueueNumber
    checkedInAppointments.sort((a, b) => (a.queueNumber || 999) - (b.queueNumber || 999));

    queueContainer.innerHTML = checkedInAppointments.map(item => {
      const p = patientsList.find(pt => pt.id === item.patientId);
      const pName = p ? p.fullName : (item.patientName || `Bệnh nhân #${item.patientId}`);
      const pPhone = p ? p.phone : '';

      return `
        <div class="queue-card">
          <div style="display:flex;align-items:center;gap:14px;">
            <div class="queue-stt">STT #${item.queueNumber || '---'}</div>
            <div class="queue-info">
              <strong>${escapeHtml(pName)} ${pPhone ? `(${escapeHtml(pPhone)})` : ''}</strong>
              <small>Mã lịch: #${item.id} · Lý do đến khám: <b>${escapeHtml(item.reason)}</b></small>
            </div>
          </div>
          <button class="examine-btn" onclick="selectPatientForExam(${item.id}, ${item.patientId}, '${escapeJs(pName)}')">
            🩺 Gọi vào khám
          </button>
        </div>
      `;
    }).join('');
  } catch (err) {
    queueContainer.innerHTML = `<p class="label">${escapeHtml(err.message)}</p>`;
  }
}

window.selectPatientForExam = async (appointmentId, patientId, patientName) => {
  document.querySelector('#exam-appointment-id').value = appointmentId;
  document.querySelector('#exam-patient-id').value = patientId;
  document.querySelector('#exam-patient-name').value = patientName;
  
  // Hiển thị lịch sử khám cũ
  const historyContainer = document.querySelector('#patient-history-container');
  const historyPreview = document.querySelector('#patient-history-preview');
  
  try {
    const patientRecords = allMedicalRecords.filter(r => r.patientId === patientId);
    if (patientRecords && patientRecords.length) {
      historyPreview.innerHTML = patientRecords.map(r => `
        <div style="border-bottom:1px solid #e2e8f0;padding-bottom:6px;margin-bottom:6px;">
          <strong>Ngày: ${new Date(r.examinedAt).toLocaleDateString('vi-VN')}</strong> · BS khám: #${r.doctorId}<br>
          <span style="color:#0f172a;">Chẩn đoán: <b>${escapeHtml(r.diagnosis)}</b></span><br>
          <small style="color:#64748b;">Triệu chứng: ${escapeHtml(r.symptoms)} | Huyết áp: ${escapeHtml(r.bloodPressure || '—')}</small>
        </div>
      `).join('');
      historyContainer.style.display = 'block';
    } else {
      historyPreview.innerHTML = '<em>Bệnh nhân chưa có lịch sử khám trước đây tại phòng khám (Bệnh nhân mới).</em>';
      historyContainer.style.display = 'block';
    }
  } catch {
    historyContainer.style.display = 'none';
  }

  document.querySelector('#exam-symptoms').focus();
  document.querySelector('#exam-card').scrollIntoView({ behavior: 'smooth' });
  doctorMessage.textContent = `✓ Đã gọi bệnh nhân: ${patientName}. Mời bác sĩ thực hiện khám và nhập kết quả.`;
};

async function loadMedicalRecords() {
  const recordsContainer = document.querySelector('#records');
  try {
    const records = await request('/api/medical-records');
    allMedicalRecords = records || [];

    // Cập nhật metrics
    const totalRecEl = document.querySelector('#doc-total-records');
    const compEl = document.querySelector('#doc-completed-count');
    if (totalRecEl) totalRecEl.textContent = records.length;
    
    const todayStr = new Date().toDateString();
    const todayCompleted = records.filter(r => new Date(r.examinedAt).toDateString() === todayStr).length;
    if (compEl) compEl.textContent = todayCompleted;

    if (!records || !records.length) {
      recordsContainer.innerHTML = '<p class="label">Chưa có hồ sơ khám bệnh nào.</p>';
      return;
    }

    recordsContainer.innerHTML = records.map(item => {
      const p = patientsList.find(pt => pt.id === item.patientId);
      const pName = p ? p.fullName : `Bệnh nhân #${item.patientId}`;

      let vitalsHtml = '';
      if (item.bloodPressure || item.heartRate || item.temperature || item.weight) {
        vitalsHtml = `
          <div style="background:#f1f5f9;padding:6px 10px;border-radius:6px;margin:6px 0;font-size:0.82rem;color:#334155;">
            🩺 <b>Sinh hiệu:</b> 
            ${item.bloodPressure ? `HA: <b>${escapeHtml(item.bloodPressure)}</b> | ` : ''}
            ${item.heartRate ? `Mạch: <b>${escapeHtml(item.heartRate)}</b> | ` : ''}
            ${item.temperature ? `Nhiệt độ: <b>${escapeHtml(item.temperature)}</b> | ` : ''}
            ${item.weight ? `Cân nặng: <b>${escapeHtml(item.weight)}</b>` : ''}
          </div>
        `;
      }

      let extraHtml = '';
      if (item.labTests) extraHtml += `<br>🧪 <b>Cận lâm sàng:</b> ${escapeHtml(item.labTests)}`;
      if (item.treatmentPlan) extraHtml += `<br>📋 <b>Hướng điều trị:</b> ${escapeHtml(item.treatmentPlan)}`;
      if (item.followUpDate) extraHtml += `<br>🗓️ <b>Hẹn tái khám:</b> ${new Date(item.followUpDate).toLocaleDateString('vi-VN')}`;

      return `
        <div class="item" style="flex-direction:column;align-items:stretch;">
          <div style="display:flex;justify-content:space-between;align-items:flex-start;">
            <div>
              <strong>Hồ sơ #${item.id} · ${escapeHtml(pName)} (Mã BN #${item.patientId})</strong>
              <small style="color:#64748b;display:block;">Bác sĩ phụ trách ID #${item.doctorId} · Lịch hẹn #${item.appointmentId}</small>
            </div>
            <span class="tag" style="background:#e0f2fe;color:#0369a1;font-weight:600;">
              ${new Date(item.examinedAt).toLocaleDateString('vi-VN')}
            </span>
          </div>

          ${vitalsHtml}

          <div style="font-size:0.88rem;color:#1e293b;margin-top:4px;">
            <b>Chẩn đoán:</b> ${escapeHtml(item.diagnosis)}<br>
            <b>Triệu chứng:</b> ${escapeHtml(item.symptoms)}
            ${extraHtml}
            ${item.notes ? `<br><b>Ghi chú:</b> <i>${escapeHtml(item.notes)}</i>` : ''}
          </div>
        </div>
      `;
    }).join('');
  } catch (err) {
    recordsContainer.innerHTML = '<p class="label">Chưa có lịch sử bệnh án.</p>';
  }
}
window.loadQueue = loadQueue;
window.loadMedicalRecords = loadMedicalRecords;

document.querySelector('#complete-exam-form')?.addEventListener('submit', async event => {
  event.preventDefault();
  const appointmentId = document.querySelector('#exam-appointment-id').value;
  if (!appointmentId) {
    doctorMessage.style.color = '#dc2626';
    doctorMessage.textContent = 'Vui lòng chọn một bệnh nhân từ Hàng chờ trước khi bấm Hoàn thành khám!';
    return;
  }

  try {
    doctorMessage.style.color = '#0284c7';
    doctorMessage.textContent = 'Đang lưu hồ sơ bệnh án và tạo đơn thuốc...';

    const followUpVal = document.querySelector('#exam-followup').value;
    const payload = {
      symptoms: document.querySelector('#exam-symptoms').value.trim(),
      diagnosis: document.querySelector('#exam-diagnosis').value.trim(),
      notes: document.querySelector('#exam-notes').value.trim() || null,
      medicines: document.querySelector('#exam-medicines').value.trim(),
      instructions: document.querySelector('#exam-instructions').value.trim(),
      bloodPressure: document.querySelector('#exam-bp').value.trim() || null,
      heartRate: document.querySelector('#exam-hr').value.trim() || null,
      temperature: document.querySelector('#exam-temp').value.trim() || null,
      weight: document.querySelector('#exam-weight').value.trim() || null,
      treatmentPlan: document.querySelector('#exam-treatment-plan').value.trim() || null,
      labTests: document.querySelector('#exam-lab-tests').value.trim() || null,
      followUpDate: followUpVal ? new Date(followUpVal).toISOString() : null
    };

    await request(`/api/appointments/${appointmentId}/complete`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    event.target.reset();
    document.querySelector('#exam-appointment-id').value = '';
    document.querySelector('#exam-patient-id').value = '';
    document.querySelector('#exam-patient-name').value = '';
    document.querySelector('#patient-history-container').style.display = 'none';

    doctorMessage.style.color = '#059669';
    doctorMessage.textContent = '★ Đã hoàn tất ca khám bệnh, lưu bệnh án và kê đơn điện tử thành công! Dữ liệu đã chuyển sang bộ phận Thu ngân & Bệnh nhân.';
    await Promise.all([loadQueue(), loadMedicalRecords()]);
  } catch (error) {
    doctorMessage.style.color = '#dc2626';
    doctorMessage.textContent = error.message;
  }
});

let doctorProfileId = 1;
async function resolveDoctorId() {
  const docs = await request('/api/doctors').catch(() => []);
  if (currentDoctorUser && docs.length) {
    const matched = docs.find(d => d.fullName.toLowerCase().includes(currentDoctorUser.fullName.toLowerCase()) || currentDoctorUser.fullName.toLowerCase().includes(d.fullName.toLowerCase()));
    if (matched) doctorProfileId = matched.id;
    else doctorProfileId = docs[0].id;
  }
}

async function loadLeaveRequests() {
  const container = document.querySelector('#leave-history');
  if (!container) return;
  try {
    const list = await request(`/api/doctor-leaves?doctorId=${doctorProfileId}`);
    if (!list || !list.length) {
      container.innerHTML = '<p class="label">Chưa có yêu cầu xin nghỉ nào.</p>';
      return;
    }

    container.innerHTML = list.map(item => {
      let badge = '<span class="tag pending">⏳ Chờ duyệt</span>';
      if (item.status === 'Approved' || item.status === 1) badge = '<span class="tag confirmed">✓ Đã chấp thuận</span>';
      if (item.status === 'Rejected' || item.status === 2) badge = '<span class="tag cancelled">✕ Từ chối</span>';

      return `
        <div class="item">
          <div>
            <strong>Ngày nghỉ: ${new Date(item.leaveDate).toLocaleDateString('vi-VN')} · Ca: ${escapeHtml(item.shift)}</strong>
            <small>Lý do: ${escapeHtml(item.reason)}<br>Gửi lúc: ${new Date(item.createdAt).toLocaleString('vi-VN')}</small>
          </div>
          <div>${badge}</div>
        </div>
      `;
    }).join('');
  } catch (e) {
    container.innerHTML = '<p class="label">Không thể tải danh sách xin nghỉ.</p>';
  }
}
window.loadLeaveRequests = loadLeaveRequests;

document.querySelector('#leave-request-form')?.addEventListener('submit', async event => {
  event.preventDefault();
  const leaveMsg = document.querySelector('#leave-message');
  try {
    const payload = {
      doctorId: doctorProfileId,
      leaveDate: document.querySelector('#leave-date').value,
      shift: document.querySelector('#leave-shift').value,
      reason: document.querySelector('#leave-reason').value
    };

    await request('/api/doctor-leaves', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    });

    event.target.reset();
    leaveMsg.style.color = '#059669';
    leaveMsg.textContent = '✓ Đã gửi yêu cầu xin nghỉ thành công tới Ban Giám Đốc!';
    await loadLeaveRequests();
  } catch (err) {
    leaveMsg.style.color = '#b44938';
    leaveMsg.textContent = err.message;
  }
});

async function loadDoctorReviews() {
  const container = document.querySelector('#doctor-reviews');
  if (!container) return;
  try {
    const summary = await request(`/api/reviews/doctor/${doctorProfileId}`);
    if (!summary || !summary.reviews || !summary.reviews.length) {
      container.innerHTML = '<p class="label">Bác sĩ chưa có đánh giá nào từ bệnh nhân.</p>';
      return;
    }

    const stars = '★'.repeat(Math.round(summary.averageRating || 5)) + '☆'.repeat(5 - Math.round(summary.averageRating || 5));
    let html = `
      <div style="background:#f8fafc;padding:12px;border-radius:8px;margin-bottom:12px;display:flex;align-items:center;gap:12px;">
        <span style="font-size:1.4rem;color:#f59e0b;font-weight:700;">${stars} ${summary.averageRating.toFixed(1)} / 5</span>
        <span style="color:#64748b;">(${summary.totalReviews} lượt đánh giá từ người bệnh)</span>
      </div>
    `;

    html += summary.reviews.map(r => `
      <div class="item">
        <div>
          <strong>${escapeHtml(r.patientName || 'Bệnh nhân')} · <span style="color:#f59e0b;">${'★'.repeat(r.rating)}${'☆'.repeat(5 - r.rating)}</span></strong>
          <small>Lịch khám #${r.appointmentId} · Ngày: ${new Date(r.createdAt).toLocaleDateString('vi-VN')}<br>${escapeHtml(r.comment || 'Không có bình luận')}</small>
        </div>
      </div>
    `).join('');

    container.innerHTML = html;
  } catch (e) {
    container.innerHTML = '<p class="label">Chưa có đánh giá nào.</p>';
  }
}
window.loadDoctorReviews = loadDoctorReviews;

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

function initDoctorSignalR() {
  if (typeof signalR === 'undefined') return;
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/hubs/clinic')
    .withAutomaticReconnect()
    .build();

  connection.on('PATIENT_WAITING', (payload) => {
    doctorMessage.style.color = '#059669';
    doctorMessage.textContent = `🔔 Bệnh nhân ${payload.patientName || ''} (STT #${payload.queueNumber || ''}) vừa Check-in vào hàng chờ!`;
    loadQueue();
  });

  connection.start().catch(err => console.log('Doctor SignalR reconnecting...', err));
}

Promise.all([loadUser(), loadPatientsList()])
  .then(() => resolveDoctorId())
  .then(() => Promise.all([loadQueue(), loadMedicalRecords(), loadLeaveRequests(), loadDoctorReviews()]))
  .then(() => initDoctorSignalR())
  .catch(() => { location.href = '/login.html'; });
