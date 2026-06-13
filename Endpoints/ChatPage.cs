using Microsoft.Extensions.Options;
using SahlAI.Api.Services;

namespace SahlAI.Api;

/// <summary>
/// Serves a self-contained WhatsApp-style web chat at "/" so the bot can be
/// demoed in a browser without configuring the WhatsApp Cloud API.
/// It posts to the existing /api/chat/test endpoint.
/// </summary>
public static class ChatPage
{
    public static IEndpointRouteBuilder MapChatDemo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", (IOptions<BotOptions> bot) =>
            Results.Content(Html.Replace("{{BUSINESS}}", bot.Value.BusinessName), "text/html; charset=utf-8"));
        return app;
    }

    private const string Html =
"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1">
<title>{{BUSINESS}} — AI Assistant</title>
<style>
  :root { --green:#075E54; --light:#25D366; --bg:#ECE5DD; }
  * { box-sizing:border-box; margin:0; padding:0; }
  body { font-family:-apple-system,"Segoe UI",Roboto,Helvetica,Arial,sans-serif; background:#0b141a; height:100vh; display:flex; align-items:center; justify-content:center; }
  .phone { width:100%; max-width:430px; height:100vh; max-height:920px; display:flex; flex-direction:column; background:var(--bg); box-shadow:0 0 40px rgba(0,0,0,.45); overflow:hidden; }
  header { background:var(--green); color:#fff; padding:14px 16px; display:flex; align-items:center; gap:12px; }
  .avatar { width:42px; height:42px; border-radius:50%; background:var(--light); display:flex; align-items:center; justify-content:center; font-weight:700; font-size:18px; flex-shrink:0; }
  header .meta h1 { font-size:16px; font-weight:600; }
  header .meta p { font-size:12px; opacity:.85; margin-top:2px; }
  #chat { flex:1; overflow-y:auto; padding:16px 14px; display:flex; flex-direction:column; gap:8px; }
  .msg { max-width:82%; padding:8px 12px; border-radius:9px; font-size:14.5px; line-height:1.45; white-space:pre-wrap; word-wrap:break-word; box-shadow:0 1px 1px rgba(0,0,0,.12); }
  .bot { align-self:flex-start; background:#fff; border-top-left-radius:2px; }
  .me { align-self:flex-end; background:#DCF8C6; border-top-right-radius:2px; }
  .typing { align-self:flex-start; background:#fff; padding:13px 16px; border-radius:9px; display:none; }
  .typing span { display:inline-block; width:7px; height:7px; margin:0 2px; background:#9a9a9a; border-radius:50%; animation:bounce 1.2s infinite; }
  .typing span:nth-child(2){ animation-delay:.2s; } .typing span:nth-child(3){ animation-delay:.4s; }
  @keyframes bounce { 0%,60%,100%{ transform:translateY(0); opacity:.4; } 30%{ transform:translateY(-6px); opacity:1; } }
  footer { padding:10px; background:#F0F0F0; display:flex; gap:8px; align-items:center; }
  #text { flex:1; border:none; border-radius:22px; padding:12px 16px; font-size:15px; outline:none; }
  #send { width:46px; height:46px; border:none; border-radius:50%; background:var(--light); color:#fff; font-size:19px; cursor:pointer; flex-shrink:0; transition:opacity .2s; }
  #send:disabled { opacity:.5; cursor:default; }
</style>
</head>
<body>
<div class="phone">
  <header>
    <div class="avatar">S</div>
    <div class="meta">
      <h1>{{BUSINESS}}</h1>
      <p>online &bull; AI assistant</p>
    </div>
  </header>
  <div id="chat">
    <div class="msg bot">Hello! 👋 I'm the AI assistant for {{BUSINESS}}. How can I help you today? You can chat in English, العربية, or हिन्दी.</div>
    <div class="typing" id="typing"><span></span><span></span><span></span></div>
  </div>
  <footer>
    <input id="text" placeholder="Type a message" autocomplete="off">
    <button id="send" aria-label="Send">&#10148;</button>
  </footer>
</div>
<script>
  const chat=document.getElementById('chat'), input=document.getElementById('text'),
        btn=document.getElementById('send'), typing=document.getElementById('typing');
  const userId='web-'+Math.random().toString(36).slice(2);
  const isArabic=s=>/[؀-ۿ]/.test(s);
  function add(text,cls){
    const d=document.createElement('div');
    d.className='msg '+cls;
    if(isArabic(text)) d.style.direction='rtl';
    d.textContent=text;
    chat.insertBefore(d,typing);
    chat.scrollTop=chat.scrollHeight;
  }
  async function send(){
    const text=input.value.trim(); if(!text) return;
    add(text,'me'); input.value=''; btn.disabled=true;
    typing.style.display='block'; chat.scrollTop=chat.scrollHeight;
    try{
      const r=await fetch('/api/chat/test',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({from:userId,text:text})});
      const data=await r.json();
      typing.style.display='none';
      add(data.reply||'(no reply)','bot');
    }catch(e){
      typing.style.display='none';
      add('⚠️ Connection error. Please try again.','bot');
    }
    btn.disabled=false; input.focus();
  }
  btn.onclick=send;
  input.addEventListener('keydown',e=>{ if(e.key==='Enter'){ e.preventDefault(); send(); } });
  input.focus();
</script>
</body>
</html>
""";
}
