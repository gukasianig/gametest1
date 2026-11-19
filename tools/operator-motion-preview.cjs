// Package full PNG frames into an APNG. No pixel processing or external dependencies.
const fs=require('fs'),path=require('path');
const root=path.resolve(__dirname,'..');
const table=Array.from({length:256},(_,n)=>{for(let k=0;k<8;k++)n=n&1?0xedb88320^(n>>>1):n>>>1;return n>>>0;});
function crc(data){let c=0xffffffff;for(const byte of data)c=table[(c^byte)&255]^(c>>>8);return (c^0xffffffff)>>>0;}
function chunk(type,data){const name=Buffer.from(type),size=Buffer.alloc(4),check=Buffer.alloc(4);size.writeUInt32BE(data.length);check.writeUInt32BE(crc(Buffer.concat([name,data])));return Buffer.concat([size,name,data,check]);}
function read(file){const png=fs.readFileSync(file);let header,ids=[];for(let p=8;p<png.length;){const size=png.readUInt32BE(p),type=png.toString('ascii',p+4,p+8),data=png.subarray(p+8,p+8+size);if(type==='IHDR')header=data;if(type==='IDAT')ids.push(data);p+=size+12;}return {header,data:Buffer.concat(ids)};}
const frames=Array.from({length:16},(_,i)=>read(path.join(root,'Logs/operator-v3-run',`frame-${String(i).padStart(2,'0')}.png`)));
const actl=Buffer.alloc(8);actl.writeUInt32BE(frames.length);
const output=[Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',frames[0].header),chunk('acTL',actl)];
let seq=0;
for(let i=0;i<frames.length;i++){
 if(!frames[i].header.equals(frames[0].header))throw Error('Frame dimensions or format differ');
 const control=Buffer.alloc(26);control.writeUInt32BE(seq++);control.writeUInt32BE(frames[0].header.readUInt32BE(0),4);control.writeUInt32BE(frames[0].header.readUInt32BE(4),8);control.writeUInt16BE(1,20);control.writeUInt16BE(25,22);
 output.push(chunk('fcTL',control));
 if(i===0)output.push(chunk('IDAT',frames[i].data));else{const counter=Buffer.alloc(4);counter.writeUInt32BE(seq++);output.push(chunk('fdAT',Buffer.concat([counter,frames[i].data])));}
}
output.push(chunk('IEND',Buffer.alloc(0)));
const target=path.join(root,'docs/art/HeroOperator/Blender/operator-run-preview.png');fs.writeFileSync(target,Buffer.concat(output));
console.log('Created 16-frame APNG, 25 fps, '+fs.statSync(target).size+' bytes');
