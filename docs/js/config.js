/* Cấu hình chung: SỬA USER, REPO, FILE cho đúng với repo của bạn */
/* ====== SỬA 3 DÒNG NÀY CHO ĐÚNG VỚI REPO CỦA BẠN ====== */
const USER = "vinhnguyen28";
const REPO = "KnapsackChallenge";
const FILE = "KnapsackChallenge-Setup.exe"; // đúng tên file cài đặt trong Releases
/* ======================================================= */

const repo = `https://github.com/${USER}/${REPO}`;
const api = `https://api.github.com/repos/${USER}/${REPO}`;
const $ = s => document.querySelector(s), $$ = s => document.querySelectorAll(s);
