const message = document.querySelector('#message');

async function request(url, options = {}) {
  const response = await fetch(url, options);
  const data = await response.json().catch(() => null);
  if (!response.ok) throw new Error(data?.message || 'Không thể thực hiện yêu cầu.');
  return data;
}

async function loadUser() {
  try {
    const user = await request('/api/auth/me');
    document.querySelector('#welcome').textContent = `Xin chào ${user.fullName} (${user.role}).`;
  } catch { window.location.href = '/login.html'; }
}

document.querySelector('#logout')?.addEventListener('click', async () => {
  await request('/api/auth/logout', { method: 'POST' });
  window.location.href = '/login.html';
});

document.querySelector('#dashboard')?.addEventListener('click', async () => {
  try {
    const summary = await request('/api/dashboard');
    message.textContent = `Bệnh nhân: ${summary.totalPatients} | Bác sĩ: ${summary.totalDoctors} | Lịch hôm nay: ${summary.todayAppointments}`;
  } catch (error) { message.textContent = error.message; }
});

document.querySelector('#users')?.addEventListener('click', async () => {
  try {
    const users = await request('/api/users');
    message.textContent = `Hệ thống hiện có ${users.length} tài khoản.`;
  } catch (error) { message.textContent = error.message; }
});

document.querySelector('#appointments')?.addEventListener('click', async () => {
  try {
    const appointments = await request('/api/appointments');
    message.textContent = `Có ${appointments.length} lịch hẹn trong hệ thống.`;
  } catch (error) { message.textContent = error.message; }
});

loadUser();
