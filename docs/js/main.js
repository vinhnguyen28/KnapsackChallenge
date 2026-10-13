/* Gán link tải, link GitHub và năm ở chân trang */
// Link tải: luôn trỏ đến bản mới nhất, bấm là tải thẳng
$$(".dl-link").forEach(a => { if (a.getAttribute("href") === "#") a.href = `${repo}/releases/latest/download/${FILE}`; });
$$(".repo-link").forEach(a => a.href = repo);
$$(".rel-link").forEach(a => a.href = `${repo}/releases`);
$$(".iss-link").forEach(a => a.href = `${repo}/issues`);
$("#yr").textContent = new Date().getFullYear();
