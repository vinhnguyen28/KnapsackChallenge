/* Màn chơi thử trên trang chủ */
// Chơi thử
const CAP = 15;
const data = [["📖","Sách",3,6],["💻","Laptop",5,12],["🔦","Đèn pin",2,4],["🍫","Đồ ăn",4,7],["📷","Máy ảnh",6,13],["🧥","Áo khoác",3,5]];
const sel = new Set();
const box = $("#items");
data.forEach((it, i) => {
  const b = document.createElement("button");
  b.className = "item";
  b.innerHTML = `<span class="e">${it[0]}</span><b>${it[1]}</b><span>${it[2]} kg · ${it[3]} điểm</span>`;
  b.onclick = () => { sel.has(i) ? sel.delete(i) : sel.add(i); b.classList.toggle("in"); update(); };
  box.appendChild(b);
});
function best() {
  const dp = Array(CAP + 1).fill(0);
  data.forEach(([, , w, v]) => { for (let c = CAP; c >= w; c--) dp[c] = Math.max(dp[c], dp[c - w] + v); });
  return dp[CAP];
}
function update() {
  let w = 0, v = 0;
  sel.forEach(i => { w += data[i][2]; v += data[i][3]; });
  $("#w").textContent = `Nặng: ${w} / ${CAP} kg`;
  $("#v").textContent = `Giá trị: ${v}`;
  $("#bar i").style.width = Math.min(100, w / CAP * 100) + "%";
  $("#bar").classList.toggle("over", w > CAP);
  const m = $("#msg"), b = best();
  if (w > CAP) m.textContent = "Balo quá tải! Hãy bỏ bớt một món.";
  else if (v === b) m.textContent = `Tuyệt vời! ${v} điểm là đáp án tối ưu.`;
  else m.textContent = sel.size ? "Chưa tối ưu, thử đổi món khác xem." : "";
}
