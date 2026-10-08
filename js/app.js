// AcxiomCRM frontend (Bootstrap + Chart.js). Data persists in localStorage (demo only; replace with /api calls).
const $=s=>document.querySelector(s),e=s=>String(s??'').replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
const hash=async p=>crypto.subtle?[...new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(p)))].map(b=>b.toString(16).padStart(2,'0')).join(''):btoa(p);
const today=()=>new Date().toISOString().slice(0,10),inr=n=>'₹'+Math.round(n).toLocaleString('en-IN');
let db,me,mod='dashboard',q='',pg=1,tab='l',ch=[];
const save=()=>localStorage.setItem('acx',JSON.stringify(db));
const al=(m,t='danger')=>{const a=$('#al');if(a){a.innerHTML=`<div class="alert alert-${t}">${e(m)}</div>`;setTimeout(()=>a.innerHTML='',4500)}};
const audit=(action,ent,rid,o,n,actor)=>{const r=(k,v)=>['hash','pw'].includes(k)?undefined:v;db.audit.unshift({user:actor||me?.email||'-',action,ent,rid:rid??'',o:o?JSON.stringify(o,r):'',n:n?JSON.stringify(n,r):'',at:new Date().toLocaleString(),ip:'client'});save()};
const T={New:['Contacted','Unqualified','Lost'],Contacted:['Qualified','Unqualified','Lost'],Qualified:['Contacted','Converted','Lost'],Unqualified:['Contacted','Lost'],Converted:[],Lost:[]},ST=Object.keys(T),SG=['Qualification','Proposal','Negotiation','Won','Lost'];
const F=(k,l,t,r,x={})=>({k,l,t,r,...x}),ops=a=>a.map(x=>[x,x]);
const scope=k=>k=='users'?db.users:db[k].filter(r=>me.role!='SalesExecutive'||r.owner==me.id);
const uname=id=>db.users.find(u=>u.id==id)?.name||'-',cname=id=>db.customers.find(c=>c.id==id)?.name||'-';
const ownerF=F('owner','Assigned To','select',1,{n:1,o:()=>me.role=='SalesExecutive'?[[me.id,me.name]]:db.users.filter(u=>u.active).map(u=>[u.id,u.name])});
const pol=p=>/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\w\s]).{8,}$/.test(p),POL='Password needs 8+ chars with upper, lower, digit and special character.';
const M={
customers:{f:[F('name','Customer Name','text',1),F('email','Email','email',1),F('phone','Phone (10-digit)','tel',1),F('company','Company','text',0),F('city','City','text',0,{len:50}),F('status','Status','select',1,{o:ops(['Active','Inactive'])}),ownerF],
 cols:['name','email','phone','company','status','owner'],
 rule:(d,id)=>{const r={},o=db.customers.filter(c=>c.id!=id);if(o.some(c=>c.email.toLowerCase()==d.email.toLowerCase()))r.email='A customer with this email already exists.';if(o.some(c=>c.phone==d.phone))r.phone='A customer with this phone already exists.';return r}},
leads:{f:[F('name','Lead Name','text',1),F('email','Email','email',1),F('phone','Phone (10-digit)','tel',1),F('company','Company','text',0),F('source','Source','select',1,{o:ops(['Website','Referral','Cold Call','Event','Social'])}),F('status','Status','select',1,{o:ops(ST)}),F('value','Expected Value (₹)','number',1,{min:0,max:100000000,m:'Expected value must be between 0 and 10,00,00,000.'}),ownerF],
 cols:['name','company','source','status','value','owner'],
 rule:(d,id)=>{const r={},old=id&&db.leads.find(l=>l.id==id);if(!ST.includes(d.status))r.status='Invalid status.';else if(old&&old.status!=d.status&&!T[old.status].includes(d.status))r.status=`Cannot move from ${old.status} to ${d.status}.`;else if(!old&&d.status!='New')r.status='New leads must start as New.';if(d.status=='Converted'&&old?.status!='Converted')r.status='Use the Convert action.';return r}},
opps:{f:[F('name','Opportunity Name','text',1),F('cust','Customer','select',1,{n:1,o:()=>scope('customers').map(c=>[c.id,c.name])}),F('amount','Amount (₹)','number',1),F('stage','Stage','select',1,{o:ops(SG)}),F('prob','Probability %','number',1,{min:0,max:100,m:'Probability must be between 0 and 100.'}),F('close','Expected Close Date','date',1),ownerF],
 cols:['name','cust','amount','stage','prob','close','owner'],
 rule:d=>{const r={},act=!['Won','Lost'].includes(d.stage);if(d.amount<0||(act&&d.amount<=0))r.amount='Opportunity Amount must be greater than 0.';if(act&&d.close<today())r.close='Expected Close Date cannot be in the past.';return r}},
fups:{f:[F('rel','Related To','select',1,{o:()=>[...scope('customers').map(c=>['c:'+c.id,'Customer: '+c.name]),...scope('leads').map(l=>['l:'+l.id,'Lead: '+l.name])]}),F('date','Follow-Up Date','date',1),F('type','Type','select',1,{o:ops(['Call','Meeting','Email'])}),F('status','Status','select',1,{o:ops(['Planned','Completed','Missed','Cancelled'])}),F('notes','Remarks','text',0,{len:250}),ownerF],
 cols:['rel','date','type','status','notes','owner'],
 rule:d=>d.status=='Planned'&&d.date<today()?{date:'Follow-up date cannot be earlier than today.'}:{}},
acts:{f:[F('type','Type','select',1,{o:ops(['Call','Meeting','Email','Task'])}),F('subject','Subject','text',1),F('date','Date','date',1),F('status','Status','select',1,{o:ops(['Open','Done'])}),ownerF],
 cols:['type','subject','date','status','owner'],rule:()=>({})},
users:{f:[F('name','Name','text',1,{len:80}),F('email','Email','email',1),F('role','Role','select',1,{o:ops(['Admin','Manager','SalesExecutive'])}),F('pw','Password','password',1,{c:1})],
 cols:['name','email','role','active','lock'],
 rule:(d,id)=>{const r={};if(db.users.some(u=>u.id!=id&&u.email.toLowerCase()==d.email.toLowerCase()))r.email='Email already registered.';if(!id&&!pol(d.pw))r.pw=POL;return r}}};
const TITLE={customers:'Customers',leads:'Leads',opps:'Opportunities',fups:'Follow-Ups',acts:'Activities',users:'Users & Roles'},LB={active:'Active',lock:'Lockout'};
const V={email:/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/,tel:/^[6-9]\d{9}$/};
function chk(f,v){
 if(f.r&&v==='')return f.l+' is required.';if(v==='')return '';
 if(f.t=='email'&&!V.email.test(v))return 'Enter a valid email address.';
 if(f.t=='tel'&&!V.tel.test(v))return 'Enter a valid phone number.';
 if(f.t=='number'){const n=+v;if(isNaN(n))return 'Enter a valid number.';if((f.min!=null&&n<f.min)||(f.max!=null&&n>f.max))return f.m}
 if(f.t=='date'&&isNaN(Date.parse(v)))return 'Enter a valid date.';
 const L=f.len||(f.t=='text'?100:0);if(L&&v.length>L)return `${f.l} must be at most ${L} characters.`;return ''}
function cell(k,r){const v=r[k];
 if(k=='owner')return uname(v);if(k=='cust')return cname(v);if(k=='rel')return v.startsWith('c:')?'Customer: '+cname(+v.slice(2)):'Lead: '+(db.leads.find(l=>l.id==+v.slice(2))?.name||'-');
 if(k=='active')return v?'Yes':'No';if(k=='lock')return r.lockEnd>Date.now()?'Locked':'-';
 if(k=='amount'||k=='value')return inr(v);if(k=='prob')return v+'%';return v??''}
// ---------- auth ----------
function auth(){$('#app').innerHTML=`<div class="container" style="max-width:440px"><h2 class="text-center mt-5 text-primary fw-bold">AcxiomCRM</h2><div class="card p-4 shadow-sm">
<ul class="nav nav-tabs mb-3">${[['l','Login'],['r','Register']].map(t=>`<li class="nav-item"><a class="nav-link ${tab==t[0]?'active':''}" href="#" onclick="tab='${t[0]}';render();return false">${t[1]}</a></li>`).join('')}</ul><div id="al"></div>
${tab=='l'?`<form onsubmit="login(event)"><input id="le" class="form-control mb-2" placeholder="Email"><input id="lp" type="password" class="form-control mb-3" placeholder="Password"><button class="btn btn-primary w-100">Login</button></form>
<div class="small text-muted mt-3">Demo: admin@acxiom.com / Admin@123<br>manager@acxiom.com / Manager@123<br>sales@acxiom.com / Sales@123<br><a href="#" onclick="localStorage.clear();sessionStorage.clear();location.reload()">Reset demo data</a></div>`
:`<form onsubmit="reg(event)"><input id="rn" class="form-control mb-2" placeholder="Full name"><input id="re" class="form-control mb-2" placeholder="Email"><input id="rp" type="password" class="form-control mb-2" placeholder="Password"><input id="rc" type="password" class="form-control mb-3" placeholder="Confirm password"><button class="btn btn-success w-100">Register</button></form>`}</div></div>`}
async function login(ev){ev.preventDefault();const em=$('#le').value.trim().toLowerCase(),p=$('#lp').value,u=db.users.find(x=>x.email.toLowerCase()==em);
 if(!em||!p)return al('Email and password are required.');
 if(u&&u.lockEnd>Date.now()){audit('FailedLogin','Auth',u.id,null,{reason:'locked'},em);return al('Account locked. Try again in a few minutes.')}
 if(u&&u.active&&u.hash==await hash(p)){u.fails=0;me=u;sessionStorage.setItem('acxu',u.id);audit('Login','Auth',u.id);return go('dashboard')}
 if(u&&++u.fails>=3){u.lockEnd=Date.now()+300000;u.fails=0;audit('Lockout','Auth',u.id,null,null,em)}
 audit('FailedLogin','Auth',u?.id,null,null,em);al('Invalid credentials or inactive account.')}
async function reg(ev){ev.preventDefault();const n=$('#rn').value.trim(),em=$('#re').value.trim(),p=$('#rp').value;
 if(!n||!em||!p)return al('All fields are required.');if(!V.email.test(em))return al('Enter a valid email address.');
 if(!pol(p))return al(POL);if(p!=$('#rc').value)return al('Passwords do not match.');
 if(db.users.some(u=>u.email.toLowerCase()==em.toLowerCase()))return al('Email already registered.');
 const u={id:++db.seq,name:n,email:em,role:'SalesExecutive',hash:await hash(p),active:true,fails:0,lockEnd:0};db.users.push(u);audit('Register','Auth',u.id,null,{email:em},em);tab='l';render();al('Registered. Please login.','success')}
function logout(){audit('Logout','Auth',me.id);me=null;sessionStorage.removeItem('acxu');render()}
// ---------- shell ----------
function go(m){if((m=='users'||m=='audit')&&me.role!='Admin')m='dashboard';mod=m;q='';pg=1;render()}
function render(){if(!me)return auth();
 const nav=[['dashboard','Dashboard'],...Object.keys(TITLE).filter(k=>k!='users').map(k=>[k,TITLE[k]])];if(me.role=='Admin')nav.push(['users','Users & Roles'],['audit','Audit Log']);
 $('#app').innerHTML=`<nav class="navbar navbar-dark bg-primary px-3"><span class="navbar-brand fw-bold">AcxiomCRM</span><span class="text-white small">${e(me.name)} <span class="badge bg-light text-dark">${me.role}</span> <button class="btn btn-sm btn-outline-light ms-2" onclick="logout()">Logout</button></span></nav>
<div class="container-fluid"><div class="row"><div class="col-md-2 p-3 bg-white border-end min-vh-100"><div class="list-group">${nav.map(n=>`<a href="#" class="list-group-item list-group-item-action ${mod==n[0]?'active':''}" onclick="go('${n[0]}');return false">${n[1]}</a>`).join('')}</div></div><div class="col-md-10 p-4"><div id="al"></div><div id="main"></div></div></div></div>`;
 mod=='dashboard'?dash():mod=='audit'?auditV():list(mod)}
// ---------- dashboard ----------
function dash(){const s=scope,L=s('leads'),O=s('opps'),open=O.filter(o=>!['Won','Lost'].includes(o.stage)),sum=a=>a.reduce((t,o)=>t+o.amount,0);
 const cards=[['Total Customers',s('customers').length],['Total Leads',L.length],['Open Leads',L.filter(l=>!['Converted','Lost','Unqualified'].includes(l.status)).length],['Total Opportunities',O.length],['Open Opportunities',open.length],['Won Opportunities',O.filter(o=>o.stage=='Won').length],['Lost Opportunities',O.filter(o=>o.stage=='Lost').length],['Pipeline Value',inr(sum(open))],['Weighted Pipeline',inr(open.reduce((t,o)=>t+o.amount*o.prob/100,0))],['Pending Follow-Ups',s('fups').filter(f=>f.status=='Planned').length]];
 $('#main').innerHTML=`<h4 class="mb-3">Dashboard <small class="text-muted fs-6">(${me.role=='SalesExecutive'?'your assigned records':'all records'})</small></h4><div class="row g-3 mb-4">${cards.map(c=>`<div class="col-6 col-lg-3"><div class="card kpi p-3"><small class="text-muted">${c[0]}</small><h4>${c[1]}</h4></div></div>`).join('')}</div>
<div class="row g-3">${['Lead Status','Opportunity Pipeline','Monthly Sales (Won)'].map((t,i)=>`<div class="col-lg-4"><div class="card p-3"><h6>${t}</h6><canvas id="c${i}"></canvas></div></div>`).join('')}</div>`;
 ch.forEach(c=>c.destroy());ch=[];if(!window.Chart)return;
 const mk=(i,type,lb,data,label)=>ch.push(new Chart($('#c'+i),{type,data:{labels:lb,datasets:[{label,data,backgroundColor:['#0d6efd','#20c997','#ffc107','#dc3545','#6f42c1','#6c757d']}]}}));
 mk(0,'doughnut',ST,ST.map(x=>L.filter(l=>l.status==x).length),'Leads');mk(1,'bar',SG,SG.map(x=>O.filter(o=>o.stage==x).length),'Opportunities');
 const ms=[...Array(6)].map((_,i)=>{const d=new Date();d.setDate(1);d.setMonth(d.getMonth()-5+i);return d.toISOString().slice(0,7)});
 mk(2,'bar',ms,ms.map(m=>sum(O.filter(o=>o.stage=='Won'&&o.close.startsWith(m)))),'Won ₹')}
// ---------- list / CRUD ----------
const can=x=>mod=='users'?me.role=='Admin':me.role!='SalesExecutive'||x.owner==me.id;
function list(k){const lb=TITLE[k];
 $('#main').innerHTML=`<div class="d-flex justify-content-between mb-3"><h4>${lb}</h4><div class="d-flex gap-2"><input class="form-control" placeholder="Search..." oninput="q=this.value;pg=1;tbl()"><button class="btn btn-primary text-nowrap" onclick="openForm('${k}')">+ Add</button></div></div><div id="tb" class="table-responsive"></div>`;tbl()}
function tbl(){const m=M[mod],lab=k=>m.f.find(f=>f.k==k)?.l||LB[k];
 let r=scope(mod).filter(x=>!q||m.cols.map(k=>cell(k,x)).join(' ').toLowerCase().includes(q.toLowerCase()));const n=Math.max(1,Math.ceil(r.length/8));pg=Math.min(pg,n);r=r.slice((pg-1)*8,pg*8);
 const B=(c,t,f)=>`<button class="btn btn-sm btn-outline-${c} me-1" onclick="${f}">${t}</button>`;
 $('#tb').innerHTML=`<table class="table table-hover bg-white align-middle"><thead><tr>${m.cols.map(k=>`<th>${lab(k)}</th>`).join('')}<th></th></tr></thead><tbody>${r.map(x=>`<tr>${m.cols.map(k=>`<td>${e(cell(k,x))}</td>`).join('')}<td class="text-nowrap">${can(x)?
 B('primary','Edit',`openForm('${mod}',${x.id})`)+(mod=='leads'&&x.status=='Qualified'?B('success','Convert',`convert(${x.id})`):'')+(mod=='fups'&&x.status=='Planned'?B('success','Complete',`complete(${x.id})`):'')
 +(mod=='users'?(x.id!=me.id?B('warning',x.active?'Deactivate':'Activate',`toggle(${x.id})`):'')+(x.lockEnd>Date.now()?B('info','Unlock',`unlock(${x.id})`):''):me.role!='SalesExecutive'?B('danger','Delete',`del('${mod}',${x.id})`):''):''}</td></tr>`).join('')||'<tr><td colspan=9 class="text-center text-muted">No records found.</td></tr>'}</tbody></table>
<div class="d-flex gap-1">${[...Array(n)].map((_,i)=>`<button class="btn btn-sm btn-${i+1==pg?'primary':'outline-primary'}" onclick="pg=${i+1};tbl()">${i+1}</button>`).join('')}</div>`}
const DEF={status:{leads:'New',fups:'Planned',acts:'Open',customers:'Active'},date:today(),close:today()};
function openForm(k,id){const m=M[k],row=id?db[k].find(r=>r.id==id):null;
 $('#mt').textContent=(id?'Edit ':'Add ')+TITLE[k];
 $('#mb').innerHTML=m.f.filter(f=>!(f.c&&id)).map(f=>{let v=row?row[f.k]??'':f.k=='owner'?me.id:f.k=='status'?DEF.status[k]||'':DEF[f.k]||'';
  const inp=f.t=='select'?`<select id="f_${f.k}" class="form-select"><option value="">-- Select --</option>${(typeof f.o=='function'?f.o():f.o).map(a=>`<option value="${e(a[0])}" ${a[0]==v?'selected':''}>${e(a[1])}</option>`).join('')}</select>`:`<input id="f_${f.k}" type="${f.t=='tel'?'text':f.t}" class="form-control" value="${e(v)}" maxlength="${f.len||(f.t=='text'?100:524288)}" ${f.t=='number'?'step="any"':''}>`;
  return `<div class="mb-3"><label class="form-label">${f.l}${f.r?' <span class="text-danger">*</span>':''}</label>${inp}<div class="invalid-feedback"></div></div>`}).join('');
 $('#fm').onsubmit=ev=>{ev.preventDefault();submit(k,id)};bootstrap.Modal.getOrCreateInstance($('#md')).show()}
async function submit(k,id){const m=M[k],d={};let er={};
 m.f.forEach(f=>{if(f.c&&id)return;const v=$('#f_'+f.k).value.trim(),x=chk(f,v);if(x)er[f.k]=x;d[f.k]=(f.t=='number'||f.n)&&v!==''?+v:v});
 if(!Object.keys(er).length)er=m.rule(d,id);
 m.f.forEach(f=>{const i=$('#f_'+f.k);if(i){i.classList.toggle('is-invalid',!!er[f.k]);i.nextElementSibling.textContent=er[f.k]||''}});
 if(Object.keys(er).length)return;
 let row=id&&db[k].find(r=>r.id==id);if(row&&!can(row))return al('Not authorized.');
 const old=row?{...row}:null;
 if(k=='users'&&!id){d.hash=await hash(d.pw);delete d.pw;Object.assign(d,{active:true,fails:0,lockEnd:0})}
 if(row)Object.assign(row,d);else{row={id:++db.seq,...d};db[k].push(row)}
 audit(id?'Update':'Create',k,row.id,old,d);if(old&&k=='users'&&old.role!=d.role)audit('RoleChange','users',row.id,{role:old.role},{role:d.role});
 bootstrap.Modal.getInstance($('#md')).hide();render();al('Saved successfully.','success')}
function del(k,id){if(me.role=='SalesExecutive'||!confirm('Delete this record?'))return;const o=db[k].find(r=>r.id==id);db[k]=db[k].filter(r=>r.id!=id);audit('Delete',k,id,o);render();al('Deleted.','success')}
function complete(id){const f=db.fups.find(x=>x.id==id);f.status='Completed';audit('Update','fups',id,{status:'Planned'},{status:'Completed'});render();al('Follow-up completed.','success')}
function toggle(id){const u=db.users.find(x=>x.id==id);u.active=!u.active;audit('Security','users',id,null,{active:u.active});render()}
function unlock(id){const u=db.users.find(x=>x.id==id);u.lockEnd=0;u.fails=0;audit('Unlock','users',id);render()}
function convert(id){const l=db.leads.find(x=>x.id==id);let c=db.customers.find(x=>x.email.toLowerCase()==l.email.toLowerCase()||x.phone==l.phone);
 if(!c){c={id:++db.seq,name:l.name,email:l.email,phone:l.phone,company:l.company,city:'',status:'Active',owner:l.owner};db.customers.push(c);audit('Create','customers',c.id,null,c)}
 const d=new Date();d.setDate(d.getDate()+30);const o={id:++db.seq,name:l.name+' - Deal',cust:c.id,amount:l.value||1,stage:'Qualification',prob:20,close:d.toISOString().slice(0,10),owner:l.owner};db.opps.push(o);
 l.status='Converted';audit('Convert','leads',id,{status:'Qualified'},{customer:c.id,opportunity:o.id});render();al('Lead converted to customer and opportunity.','success')}
function auditV(){$('#main').innerHTML=`<div class="d-flex justify-content-between mb-3"><h4>Audit Log</h4><input class="form-control w-25" placeholder="Filter user / action / module" oninput="q=this.value;ad()"></div><div id="tb" class="table-responsive"></div>`;ad()}
function ad(){const r=db.audit.filter(a=>!q||(a.user+a.action+a.ent).toLowerCase().includes(q.toLowerCase())).slice(0,100);
 $('#tb').innerHTML=`<table class="table table-sm bg-white"><thead><tr><th>Time</th><th>User</th><th>Action</th><th>Module</th><th>Record</th><th>Old</th><th>New</th><th>IP</th></tr></thead><tbody>${r.map(a=>`<tr><td>${e(a.at)}</td><td>${e(a.user)}</td><td>${e(a.action)}</td><td>${e(a.ent)}</td><td>${e(a.rid)}</td><td class="small">${e(a.o)}</td><td class="small">${e(a.n)}</td><td>${a.ip}</td></tr>`).join('')}</tbody></table>`}
// ---------- seed & boot ----------
async function seed(){const d=n=>{const x=new Date();x.setDate(x.getDate()+n);return x.toISOString().slice(0,10)},mu=async(id,name,email,role,p)=>({id,name,email,role,hash:await hash(p),active:true,fails:0,lockEnd:0});
 return{seq:100,audit:[],users:await Promise.all([mu(1,'Aarav Admin','admin@acxiom.com','Admin','Admin@123'),mu(2,'Meera Manager','manager@acxiom.com','Manager','Manager@123'),mu(3,'Sanjay Sales','sales@acxiom.com','SalesExecutive','Sales@123'),mu(4,'Priya Sales','priya@acxiom.com','SalesExecutive','Sales@123')]),
 customers:[{id:11,name:'Zenith Retail',email:'buy@zenith.in',phone:'9876543210',company:'Zenith Pvt Ltd',city:'Hyderabad',status:'Active',owner:3},{id:12,name:'Orbit Logistics',email:'ops@orbit.in',phone:'9123456780',company:'Orbit Ltd',city:'Pune',status:'Active',owner:4},{id:13,name:'BlueLeaf Foods',email:'hi@blueleaf.in',phone:'8899776655',company:'BlueLeaf',city:'Chennai',status:'Inactive',owner:3}],
 leads:[{id:21,name:'Nova Textiles',email:'nova@tex.in',phone:'9000011111',company:'Nova',source:'Website',status:'New',value:250000,owner:3},{id:22,name:'Kite Labs',email:'kite@labs.in',phone:'9000022222',company:'Kite',source:'Referral',status:'Qualified',value:900000,owner:3},{id:23,name:'Pixel Print',email:'pp@print.in',phone:'9000033333',company:'Pixel',source:'Event',status:'Contacted',value:120000,owner:4},{id:24,name:'Lotus Hotels',email:'lh@hotels.in',phone:'9000044444',company:'Lotus',source:'Cold Call',status:'Lost',value:60000,owner:4}],
 opps:[{id:31,name:'Zenith ERP',cust:11,amount:1500000,stage:'Proposal',prob:50,close:d(25),owner:3},{id:32,name:'Orbit Fleet App',cust:12,amount:800000,stage:'Negotiation',prob:70,close:d(10),owner:4},{id:33,name:'Zenith POS',cust:11,amount:400000,stage:'Won',prob:100,close:d(-20),owner:3},{id:34,name:'Orbit Portal',cust:12,amount:300000,stage:'Lost',prob:0,close:d(-40),owner:4}],
 fups:[{id:41,rel:'c:11',date:d(2),type:'Call',status:'Planned',notes:'Discuss proposal',owner:3},{id:42,rel:'l:23',date:d(5),type:'Meeting',status:'Planned',notes:'Demo',owner:4},{id:43,rel:'c:12',date:d(-3),type:'Email',status:'Completed',notes:'Sent quote',owner:4}],
 acts:[{id:51,type:'Call',subject:'Intro call with Nova',date:d(-1),status:'Done',owner:3},{id:52,type:'Task',subject:'Prepare ERP proposal',date:d(3),status:'Open',owner:3}]}}
(async()=>{db=JSON.parse(localStorage.getItem('acx')||'null')||await seed();save();const s=sessionStorage.getItem('acxu');me=s&&db.users.find(u=>u.id==s&&u.active)||null;render()})();
