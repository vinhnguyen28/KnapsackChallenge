/* Lấy phiên bản, dung lượng, lượt tải từ GitHub Releases */
// Thông tin bản phát hành
fetch(`${api}/releases/latest`).then(r => r.ok ? r.json() : Promise.reject()).then(d => {
  const a = (d.assets || []).find(x => x.name === FILE) || (d.assets || [])[0];
  const date = new Date(d.published_at).toLocaleDateString("vi-VN");
  const mb = a ? (a.size / 1048576).toFixed(1) + " MB" : "";
  $("#relinfo").textContent = `Phiên bản ${d.tag_name} · ${date} · Windows 10/11`;
  $("#dlinfo").textContent = `Phiên bản ${d.tag_name} · ${mb} · ${a ? a.download_count : 0} lượt tải`;
  if (mb) $("#size").textContent = mb;
}).catch(() => { $("#dlinfo").textContent = "Windows 10/11 · Miễn phí"; });
