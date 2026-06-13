using Microsoft.Extensions.Options;
using SahlAI.Api.Services;

namespace SahlAI.Api;

/// <summary>
/// Serves the Dubai Homes Realty marketing website at "/" with a floating
/// popup AI chat widget. The widget posts to /api/chat which logs every
/// visitor turn (name/phone/text) to the lead database.
/// </summary>
public static class ChatPage
{
    public static IEndpointRouteBuilder MapChatDemo(this IEndpointRouteBuilder app)
    {
        app.MapGet("/", (HttpContext ctx, IOptions<BotOptions> bot, ILeadStore leads) =>
        {
            var src = ctx.Request.Query["src"].ToString();
            var referrer = ctx.Request.Headers.Referer.ToString();
            var ua = ctx.Request.Headers.UserAgent.ToString();
            leads.LogVisit(
                Trim(src), ctx.Request.Path.ToString(), Trim(referrer), Trim(ua), ClientIp(ctx));
            return Results.Content(Html.Replace("{{BUSINESS}}", bot.Value.BusinessName), "text/html; charset=utf-8");
        });
        return app;
    }

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Best-effort real client IP (Azure forwards it in X-Forwarded-For as ip:port, possibly comma-separated).</summary>
    private static string? ClientIp(HttpContext ctx)
    {
        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        var raw = !string.IsNullOrWhiteSpace(xff)
            ? xff.Split(',')[0].Trim()
            : ctx.Connection.RemoteIpAddress?.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw.Count(ch => ch == ':') == 1) raw = raw.Split(':')[0]; // strip :port for IPv4
        return raw;
    }

    private const string Html =
"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{{BUSINESS}} — Luxury Real Estate in Dubai</title>
<link rel="preconnect" href="https://fonts.googleapis.com">
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Playfair+Display:wght@500;600;700&family=Inter:wght@400;500;600&display=swap" rel="stylesheet">
<style>
  :root{ --navy:#1A2A4A; --navy2:#22335c; --gold:#C9A55C; --cream:#F0ECE3; --ink:#222; }
  *{ box-sizing:border-box; margin:0; padding:0; }
  html{ scroll-behavior:smooth; }
  body{ font-family:'Inter',-apple-system,'Segoe UI',Roboto,sans-serif; color:var(--ink); background:#fff; line-height:1.6; }
  h1,h2,h3,.serif{ font-family:'Playfair Display',Georgia,'Times New Roman',serif; }
  a{ text-decoration:none; color:inherit; }
  .wrap{ max-width:1180px; margin:0 auto; padding:0 22px; }
  .btn{ display:inline-block; background:var(--gold); color:var(--navy); font-weight:600; padding:13px 28px; border-radius:4px; border:none; cursor:pointer; letter-spacing:.3px; transition:.2s; }
  .btn:hover{ background:#d8b76e; transform:translateY(-1px); }
  .btn-outline{ background:transparent; color:#fff; border:1.5px solid var(--gold); }
  .btn-outline:hover{ background:var(--gold); color:var(--navy); }

  /* NAV */
  nav{ position:fixed; top:0; left:0; right:0; z-index:50; background:rgba(26,42,74,.96); backdrop-filter:blur(6px); }
  nav .wrap{ display:flex; align-items:center; justify-content:space-between; height:70px; }
  .logo{ color:#fff; font-family:'Playfair Display',serif; font-size:22px; font-weight:700; }
  .logo b{ color:var(--gold); }
  .navlinks{ display:flex; gap:30px; align-items:center; }
  .navlinks a{ color:#e9e9e9; font-size:14.5px; font-weight:500; transition:.2s; }
  .navlinks a:hover{ color:var(--gold); }
  .menu-toggle{ display:none; background:none; border:none; color:#fff; font-size:26px; cursor:pointer; }
  @media(max-width:820px){ .navlinks{ display:none; } .menu-toggle{ display:block; } }

  /* HERO */
  header.hero{ min-height:100vh; display:flex; align-items:center; color:#fff; position:relative;
    background:linear-gradient(rgba(16,24,44,.6),rgba(16,24,44,.78)), url('https://images.unsplash.com/photo-1749273858638-ea678cb48e94?w=1900&q=80') center/cover fixed; }
  .hero .wrap{ padding-top:90px; padding-bottom:60px; }
  .hero .eyebrow{ color:var(--gold); letter-spacing:3px; text-transform:uppercase; font-size:13px; font-weight:600; margin-bottom:16px; }
  .hero h1{ font-size:clamp(34px,6vw,62px); font-weight:700; line-height:1.1; max-width:780px; }
  .hero p{ font-size:clamp(16px,2vw,20px); max-width:600px; margin:22px 0 34px; color:#e4e4e4; }
  .hero .cta-row{ display:flex; gap:14px; flex-wrap:wrap; }

  /* STATS */
  .stats{ background:var(--navy); color:#fff; }
  .stats .wrap{ display:grid; grid-template-columns:repeat(4,1fr); gap:20px; padding:42px 22px; text-align:center; }
  .stats .num{ font-family:'Playfair Display',serif; font-size:38px; color:var(--gold); font-weight:700; }
  .stats .lbl{ font-size:13.5px; letter-spacing:.5px; color:#cfd6e4; margin-top:4px; }
  @media(max-width:680px){ .stats .wrap{ grid-template-columns:repeat(2,1fr); } }

  /* SECTION */
  section{ padding:84px 0; }
  .head{ text-align:center; max-width:640px; margin:0 auto 50px; }
  .head .eyebrow{ color:var(--gold); letter-spacing:2.5px; text-transform:uppercase; font-size:12.5px; font-weight:600; }
  .head h2{ font-size:clamp(28px,4vw,42px); color:var(--navy); margin-top:10px; }
  .head p{ color:#666; margin-top:12px; }

  /* PROPERTY CARDS */
  .grid3{ display:grid; grid-template-columns:repeat(3,1fr); gap:28px; }
  @media(max-width:880px){ .grid3{ grid-template-columns:1fr; max-width:420px; margin:0 auto; } }
  .card{ background:#fff; border-radius:10px; overflow:hidden; box-shadow:0 10px 30px rgba(0,0,0,.09); transition:.25s; }
  .card:hover{ transform:translateY(-6px); box-shadow:0 18px 44px rgba(0,0,0,.16); }
  .card .img{ height:230px; background-size:cover; background-position:center; position:relative; }
  .card .tag{ position:absolute; top:14px; left:14px; background:var(--gold); color:var(--navy); font-size:12px; font-weight:700; padding:5px 12px; border-radius:3px; letter-spacing:.5px; }
  .card .body{ padding:20px 22px 24px; }
  .card .loc{ color:var(--gold); font-size:13px; font-weight:600; letter-spacing:.5px; }
  .card h3{ font-size:21px; color:var(--navy); margin:6px 0 10px; }
  .card .price{ font-family:'Playfair Display',serif; font-size:24px; color:var(--navy); font-weight:700; }
  .card .feats{ display:flex; gap:18px; margin-top:14px; padding-top:14px; border-top:1px solid #eee; color:#666; font-size:13.5px; }

  /* SERVICES */
  .services{ background:var(--cream); }
  .grid-serv{ display:grid; grid-template-columns:repeat(3,1fr); gap:24px; }
  @media(max-width:880px){ .grid-serv{ grid-template-columns:1fr 1fr; } }
  @media(max-width:520px){ .grid-serv{ grid-template-columns:1fr; } }
  .serv{ background:#fff; padding:32px 26px; border-radius:10px; border:1px solid #ece6da; transition:.2s; }
  .serv:hover{ border-color:var(--gold); box-shadow:0 10px 26px rgba(201,165,92,.18); }
  .serv .ic{ width:52px; height:52px; border-radius:50%; background:var(--navy); color:var(--gold); display:flex; align-items:center; justify-content:center; font-size:24px; margin-bottom:16px; }
  .serv h3{ font-size:19px; color:var(--navy); margin-bottom:8px; }
  .serv p{ color:#666; font-size:14.5px; }

  /* ABOUT */
  .about .wrap{ display:grid; grid-template-columns:1fr 1fr; gap:54px; align-items:center; }
  @media(max-width:880px){ .about .wrap{ grid-template-columns:1fr; } }
  .about img{ width:100%; border-radius:12px; box-shadow:0 16px 40px rgba(0,0,0,.18); }
  .about h2{ font-size:clamp(26px,3.6vw,38px); color:var(--navy); }
  .about .eyebrow{ color:var(--gold); letter-spacing:2.5px; text-transform:uppercase; font-size:12.5px; font-weight:600; }
  .about ul{ list-style:none; margin:20px 0; }
  .about li{ padding:8px 0 8px 30px; position:relative; color:#444; }
  .about li:before{ content:'✓'; position:absolute; left:0; color:var(--gold); font-weight:700; }

  /* AREAS */
  .areas{ background:var(--navy); color:#fff; }
  .areas .head h2{ color:#fff; }
  .grid-area{ display:grid; grid-template-columns:repeat(4,1fr); gap:18px; }
  @media(max-width:880px){ .grid-area{ grid-template-columns:1fr 1fr; } }
  .area{ height:180px; border-radius:9px; background-size:cover; background-position:center; position:relative; overflow:hidden; }
  .area span{ position:absolute; inset:0; display:flex; align-items:flex-end; padding:16px; font-weight:600; font-size:16px;
    background:linear-gradient(transparent,rgba(16,24,44,.85)); }

  /* CTA BANNER */
  .cta{ background:linear-gradient(rgba(26,42,74,.9),rgba(26,42,74,.9)), url('https://images.unsplash.com/photo-1745750434535-5943ef2fd31a?w=1600&q=80') center/cover; color:#fff; text-align:center; }
  .cta h2{ font-size:clamp(26px,4vw,40px); }
  .cta p{ max-width:560px; margin:14px auto 28px; color:#dfe3ec; }

  /* FOOTER */
  footer{ background:#13203a; color:#aeb6c6; padding:54px 0 26px; font-size:14px; }
  .fgrid{ display:grid; grid-template-columns:2fr 1fr 1fr 1.4fr; gap:34px; }
  @media(max-width:780px){ .fgrid{ grid-template-columns:1fr 1fr; } }
  footer h4{ color:#fff; font-size:15px; margin-bottom:14px; }
  footer .logo{ font-size:22px; margin-bottom:12px; }
  footer a{ display:block; padding:4px 0; color:#aeb6c6; }
  footer a:hover{ color:var(--gold); }
  .copy{ border-top:1px solid #243657; margin-top:36px; padding-top:18px; text-align:center; color:#7c8497; font-size:13px; }

  /* CHAT WIDGET */
  #chatBtn{ position:fixed; bottom:26px; right:26px; z-index:90; width:64px; height:64px; border-radius:50%; background:var(--gold); color:var(--navy); border:none; cursor:pointer; font-size:28px; box-shadow:0 8px 24px rgba(0,0,0,.28); display:flex; align-items:center; justify-content:center; animation:pulse 2.4s infinite; }
  @keyframes pulse{ 0%{ box-shadow:0 0 0 0 rgba(201,165,92,.6);} 70%{ box-shadow:0 0 0 16px rgba(201,165,92,0);} 100%{ box-shadow:0 0 0 0 rgba(201,165,92,0);} }
  #chatPanel{ position:fixed; bottom:26px; right:26px; z-index:91; width:380px; max-width:calc(100vw - 32px); height:560px; max-height:calc(100vh - 52px); background:#F0ECE3; border-radius:14px; box-shadow:0 18px 50px rgba(0,0,0,.32); display:none; flex-direction:column; overflow:hidden; }
  #chatPanel.open{ display:flex; }
  .cw-head{ background:var(--navy); color:#fff; padding:14px 16px; display:flex; align-items:center; gap:11px; }
  .cw-head .av{ width:40px; height:40px; border-radius:50%; background:var(--gold); color:var(--navy); display:flex; align-items:center; justify-content:center; font-weight:700; }
  .cw-head h1{ font-family:'Inter',sans-serif; font-size:15.5px; font-weight:600; }
  .cw-head p{ font-size:11.5px; opacity:.85; }
  .cw-close{ margin-left:auto; background:none; border:none; color:#fff; font-size:22px; cursor:pointer; opacity:.8; }
  .cw-close:hover{ opacity:1; }
  #cwMsgs{ flex:1; overflow-y:auto; padding:15px 13px; display:flex; flex-direction:column; gap:8px; }
  .b{ max-width:84%; padding:9px 13px; border-radius:10px; font-size:14px; line-height:1.45; white-space:pre-wrap; word-wrap:break-word; box-shadow:0 1px 1px rgba(0,0,0,.1); }
  .b.bot{ align-self:flex-start; background:#fff; border-top-left-radius:2px; }
  .b.me{ align-self:flex-end; background:#F5E9D0; border-top-right-radius:2px; }
  .cw-typing{ align-self:flex-start; background:#fff; padding:12px 15px; border-radius:10px; display:none; }
  .cw-typing span{ display:inline-block; width:7px; height:7px; margin:0 2px; background:#9a9a9a; border-radius:50%; animation:bnc 1.2s infinite; }
  .cw-typing span:nth-child(2){ animation-delay:.2s; } .cw-typing span:nth-child(3){ animation-delay:.4s; }
  @keyframes bnc{ 0%,60%,100%{ transform:translateY(0); opacity:.4; } 30%{ transform:translateY(-6px); opacity:1; } }
  .cw-foot{ padding:10px; background:#e8e3d8; display:flex; gap:8px; }
  #cwText{ flex:1; border:none; border-radius:20px; padding:11px 15px; font-size:14.5px; outline:none; }
  #cwSend{ width:44px; height:44px; border:none; border-radius:50%; background:var(--gold); color:var(--navy); font-size:18px; cursor:pointer; }
  #cwSend:disabled{ opacity:.5; }
</style>
</head>
<body>

<nav>
  <div class="wrap">
    <a href="#" class="logo">Dubai <b>Homes</b> Realty</a>
    <div class="navlinks">
      <a href="#properties">Properties</a>
      <a href="#services">Services</a>
      <a href="#areas">Communities</a>
      <a href="#about">About</a>
      <a href="#" onclick="openChat();return false;" class="btn" style="padding:9px 20px;">Chat with us</a>
    </div>
    <button class="menu-toggle" onclick="openChat()">&#9776;</button>
  </div>
</nav>

<header class="hero">
  <div class="wrap">
    <div class="eyebrow">Dubai's Premier Property Partner</div>
    <h1>Find Your Address in the City of Gold</h1>
    <p>From waterfront apartments in Dubai Marina to private villas on Palm Jumeirah — discover handpicked luxury homes with expert guidance every step of the way.</p>
    <div class="cta-row">
      <a href="#properties" class="btn">Explore Properties</a>
      <a href="#" onclick="openChat();return false;" class="btn btn-outline">Talk to our AI Advisor</a>
    </div>
  </div>
</header>

<div class="stats">
  <div class="wrap">
    <div><div class="num">AED 2B+</div><div class="lbl">Property Portfolio</div></div>
    <div><div class="num">500+</div><div class="lbl">Exclusive Listings</div></div>
    <div><div class="num">30+</div><div class="lbl">Prime Communities</div></div>
    <div><div class="num">98%</div><div class="lbl">Client Satisfaction</div></div>
  </div>
</div>

<section id="properties">
  <div class="wrap">
    <div class="head">
      <div class="eyebrow">Featured Listings</div>
      <h2>Signature Properties</h2>
      <p>A curated selection of Dubai's most sought-after residences, ready for viewing.</p>
    </div>
    <div class="grid3">
      <div class="card">
        <div class="img" style="background-image:url('https://images.unsplash.com/photo-1660217327743-31db0be68384?w=900&q=80')"><span class="tag">FOR SALE</span></div>
        <div class="body">
          <div class="loc">DUBAI MARINA</div>
          <h3>Marina Vista Residence</h3>
          <div class="price">AED 2,450,000</div>
          <div class="feats"><span>2 Beds</span><span>2 Baths</span><span>1,180 sqft</span></div>
        </div>
      </div>
      <div class="card">
        <div class="img" style="background-image:url('https://images.unsplash.com/photo-1777464888407-1c8cbacf7e8d?w=900&q=80')"><span class="tag">EXCLUSIVE</span></div>
        <div class="body">
          <div class="loc">PALM JUMEIRAH</div>
          <h3>Palm Signature Villa</h3>
          <div class="price">AED 28,500,000</div>
          <div class="feats"><span>5 Beds</span><span>6 Baths</span><span>7,400 sqft</span></div>
        </div>
      </div>
      <div class="card">
        <div class="img" style="background-image:url('https://images.unsplash.com/photo-1518733057094-95b53143d2a7?w=900&q=80')"><span class="tag">NEW</span></div>
        <div class="body">
          <div class="loc">DOWNTOWN DUBAI</div>
          <h3>Burj View Penthouse</h3>
          <div class="price">AED 6,900,000</div>
          <div class="feats"><span>3 Beds</span><span>4 Baths</span><span>2,650 sqft</span></div>
        </div>
      </div>
    </div>
  </div>
</section>

<section id="services" class="services">
  <div class="wrap">
    <div class="head">
      <div class="eyebrow">What We Do</div>
      <h2>End-to-End Property Services</h2>
      <p>Whether you are buying your first home or building a portfolio, we make it effortless.</p>
    </div>
    <div class="grid-serv">
      <div class="serv"><div class="ic">&#127968;</div><h3>Buy a Home</h3><p>Access off-plan and ready properties across Dubai with transparent pricing and payment plans.</p></div>
      <div class="serv"><div class="ic">&#127991;</div><h3>Sell Your Property</h3><p>Premium marketing and qualified buyers to sell your home faster and at the best value.</p></div>
      <div class="serv"><div class="ic">&#128273;</div><h3>Rent &amp; Lease</h3><p>Furnished and unfurnished homes in every community, with hassle-free Ejari paperwork.</p></div>
      <div class="serv"><div class="ic">&#128200;</div><h3>Invest &amp; Grow</h3><p>High-yield investment opportunities with full ROI and Golden Visa eligibility guidance.</p></div>
      <div class="serv"><div class="ic">&#127963;</div><h3>Property Management</h3><p>We handle tenants, maintenance and returns so your asset works while you relax.</p></div>
      <div class="serv"><div class="ic">&#129309;</div><h3>Mortgage Advisory</h3><p>Best mortgage rates from leading UAE banks, arranged by our in-house finance team.</p></div>
    </div>
  </div>
</section>

<section id="about" class="about">
  <div class="wrap">
    <img src="https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?w=1000&q=80" alt="Luxury Dubai interior">
    <div>
      <div class="eyebrow">Why Dubai Homes Realty</div>
      <h2>A New Standard of Trust in Dubai Property</h2>
      <p>We pair deep local market knowledge with technology — including an AI advisor available 24/7 — so every client gets clear, honest guidance in their own language.</p>
      <ul>
        <li>RERA-aligned, fully transparent transactions</li>
        <li>Multilingual team — English, العربية, हिन्दी</li>
        <li>Handpicked listings across every prime community</li>
        <li>Dedicated advisor from first viewing to handover</li>
      </ul>
      <a href="#" onclick="openChat();return false;" class="btn">Start a Conversation</a>
    </div>
  </div>
</section>

<section id="areas" class="areas">
  <div class="wrap">
    <div class="head"><div class="eyebrow">Where We Operate</div><h2>Prime Communities</h2></div>
    <div class="grid-area">
      <div class="area" style="background-image:url('https://images.unsplash.com/photo-1743368691866-018360c36fad?w=700&q=80')"><span>Downtown Dubai</span></div>
      <div class="area" style="background-image:url('https://images.unsplash.com/photo-1660217327743-31db0be68384?w=700&q=80')"><span>Dubai Marina</span></div>
      <div class="area" style="background-image:url('https://images.unsplash.com/photo-1777464888407-1c8cbacf7e8d?w=700&q=80')"><span>Palm Jumeirah</span></div>
      <div class="area" style="background-image:url('https://images.unsplash.com/photo-1745750434535-5943ef2fd31a?w=700&q=80')"><span>Business Bay</span></div>
    </div>
  </div>
</section>

<section class="cta">
  <div class="wrap">
    <h2>Your Dream Home is One Message Away</h2>
    <p>Chat with our AI property advisor now — get instant answers on prices, availability and book a viewing in seconds.</p>
    <a href="#" onclick="openChat();return false;" class="btn">Chat with our AI Advisor</a>
  </div>
</section>

<footer>
  <div class="wrap">
    <div class="fgrid">
      <div>
        <div class="logo" style="color:#fff;">Dubai <b style="color:var(--gold)">Homes</b> Realty</div>
        <p style="max-width:320px;">Luxury real estate, simplified. Buy, sell, rent and invest in Dubai with a partner you can trust.</p>
      </div>
      <div><h4>Explore</h4><a href="#properties">Properties</a><a href="#services">Services</a><a href="#areas">Communities</a><a href="#about">About</a></div>
      <div><h4>Communities</h4><a href="#">Downtown Dubai</a><a href="#">Dubai Marina</a><a href="#">Palm Jumeirah</a><a href="#">Business Bay</a></div>
      <div><h4>Get in touch</h4><a href="#" onclick="openChat();return false;">💬 Chat with our AI Advisor</a><a href="#">📍 Business Bay, Dubai, UAE</a><a href="#">✉ hello@dubaihomesrealty.ae</a></div>
    </div>
    <div class="copy">© 2026 Dubai Homes Realty. All rights reserved. &nbsp;|&nbsp; Powered by Sahl AI</div>
  </div>
</footer>

<!-- CHAT WIDGET -->
<button id="chatBtn" onclick="openChat()" aria-label="Open chat">&#128172;</button>
<div id="chatPanel">
  <div class="cw-head">
    <div class="av">D</div>
    <div><h1>Dubai Homes Realty</h1><p>online &bull; AI Property Advisor</p></div>
    <button class="cw-close" onclick="closeChat()" aria-label="Close">&times;</button>
  </div>
  <div id="cwMsgs">
    <div class="b bot">Welcome to Dubai Homes Realty! 🏠 Looking for your dream property? Ask me about listings, prices, or book a viewing — in English, العربية, or हिन्दी.</div>
    <div class="cw-typing" id="cwTyping"><span></span><span></span><span></span></div>
  </div>
  <div class="cw-foot">
    <input id="cwText" placeholder="Type your message…" autocomplete="off">
    <button id="cwSend" aria-label="Send">&#10148;</button>
  </div>
</div>

<script>
  const panel=document.getElementById('chatPanel'), chatBtn=document.getElementById('chatBtn'),
        msgs=document.getElementById('cwMsgs'), input=document.getElementById('cwText'),
        sendBtn=document.getElementById('cwSend'), typing=document.getElementById('cwTyping');
  let sessionId=localStorage.getItem('sahl_session');
  if(!sessionId){ sessionId='web-'+Math.random().toString(36).slice(2)+Date.now().toString(36); localStorage.setItem('sahl_session',sessionId); }
  const isArabic=s=>/[؀-ۿ]/.test(s);
  function openChat(){ panel.classList.add('open'); chatBtn.style.display='none'; input.focus(); }
  function closeChat(){ panel.classList.remove('open'); chatBtn.style.display='flex'; }
  function add(text,cls){
    const d=document.createElement('div'); d.className='b '+cls;
    if(isArabic(text)) d.style.direction='rtl';
    d.textContent=text; msgs.insertBefore(d,typing); msgs.scrollTop=msgs.scrollHeight;
  }
  async function send(){
    const text=input.value.trim(); if(!text) return;
    add(text,'me'); input.value=''; sendBtn.disabled=true;
    typing.style.display='block'; msgs.scrollTop=msgs.scrollHeight;
    try{
      const r=await fetch('/api/chat',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify({sessionId:sessionId,text:text})});
      const data=await r.json();
      typing.style.display='none'; add(data.reply||'(no reply)','bot');
    }catch(e){ typing.style.display='none'; add('⚠️ Connection error. Please try again.','bot'); }
    sendBtn.disabled=false; input.focus();
  }
  sendBtn.onclick=send;
  input.addEventListener('keydown',e=>{ if(e.key==='Enter'){ e.preventDefault(); send(); } });
</script>
</body>
</html>
""";
}
