/* Trình xem mã nguồn lấy dữ liệu từ GitHub API */
// Trình xem mã nguồn
let cur = "";
const ext = n => /\.(cs|xaml|csproj|sln|md|json|xml|txt|config|gitignore|yml|yaml|cshtml|html|css|js)$/i.test(n) || !n.includes(".");
async function list(path) {
  cur = path;
  $("#path").textContent = "/" + path;
  $("#tree").textContent = "Đang tải…";
  try {
    const r = await fetch(`${api}/contents/${path}`);
    if (!r.ok) throw 0;
    const arr = (await r.json()).sort((a, b) => (a.type === b.type ? a.name.localeCompare(b.name) : a.type === "dir" ? -1 : 1));
    $("#tree").innerHTML = "";
    arr.forEach(f => {
      const b = document.createElement("button");
      b.textContent = (f.type === "dir" ? "📁 " : "📄 ") + f.name;
      b.onclick = () => {
        if (f.type === "dir") list(f.path);
        else if (ext(f.name)) open(f, b);
        else window.open(f.html_url, "_blank");
      };
      $("#tree").appendChild(b);
    });
  } catch { $("#tree").innerHTML = `Không tải được. Hãy kiểm tra repo đã public chưa, hoặc <a href="${repo}" target="_blank" rel="noopener">mở trên GitHub</a>.`; }
}
async function open(f, btn) {
  $$("#tree button").forEach(x => x.classList.remove("act"));
  btn.classList.add("act");
  $("#code").textContent = "Đang tải…";
  try { $("#code").textContent = await (await fetch(f.download_url)).text(); }
  catch { $("#code").textContent = "Không đọc được tệp này."; }
}
$("#up").onclick = () => list(cur.split("/").slice(0, -1).join("/"));
list("");
