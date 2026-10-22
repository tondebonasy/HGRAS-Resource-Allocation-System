(function () {
 'use strict';
 var colours=['#0b86a7','#32b3a0','#82c8dc','#efb75c','#a2a8d4','#e27d79','#466b88'];
 var ns='http://www.w3.org/2000/svg';
 function element(name,attrs,text){var node=document.createElementNS(ns,name);Object.keys(attrs||{}).forEach(function(key){node.setAttribute(key,attrs[key]);});if(text!==undefined)node.textContent=text;return node;}
 function number(value){var n=Number(value);return Number.isFinite(n)&&n>0?n:0;}
 function render(card){
  if(card.dataset.chartRendered)return;card.dataset.chartRendered='true';
  var rows=Array.from(card.querySelectorAll('[data-chart-value]'));
  var values=rows.map(function(row,index){var dot=row.querySelector('.hgras-chart-dot');if(dot)dot.style.backgroundColor=colours[index%colours.length];return {label:row.dataset.chartLabel,value:number(row.dataset.chartValue),colour:colours[index%colours.length]};});
  var total=values.reduce(function(sum,item){return sum+item.value;},0),visual=card.querySelector('.hgras-chart-visual');
  var empty=card.querySelector('.hgras-chart-empty');if(empty)empty.hidden=total>0;
  if(!visual)return;
  if(card.dataset.hgrasChart==='bar'){
   var height=Math.max(115,values.length*41+20),svg=element('svg',{viewBox:'0 0 460 '+height}),max=Math.max.apply(null,values.map(function(x){return x.value;}).concat([1]));
   values.forEach(function(item,index){var y=index*41+14;svg.appendChild(element('text',{x:0,y:y+12,fill:'#718793','font-size':11},item.label));svg.appendChild(element('rect',{x:145,y:y,width:265,height:17,rx:5,fill:'#eff4f7'}));var bar=element('rect',{x:145,y:y,width:item.value/max*265,height:17,rx:5,fill:item.colour});bar.appendChild(element('title',{},item.label+': '+item.value));svg.appendChild(bar);svg.appendChild(element('text',{x:450,y:y+13,'text-anchor':'end',fill:'#214a60','font-size':12,'font-weight':600},item.value.toLocaleString()));});visual.appendChild(svg);
  }else{
   var chart=element('svg',{viewBox:'0 0 220 220'}),radius=77,circumference=2*Math.PI*radius,offset=0;
   chart.appendChild(element('circle',{cx:110,cy:110,r:radius,fill:'none',stroke:'#edf3f6','stroke-width':26}));
   values.forEach(function(item){if(!item.value||!total)return;var length=item.value/total*circumference;var segment=element('circle',{cx:110,cy:110,r:radius,fill:'none',stroke:item.colour,'stroke-width':26,'stroke-dasharray':length+' '+(circumference-length),'stroke-dashoffset':-offset,transform:'rotate(-90 110 110)'});segment.appendChild(element('title',{},item.label+': '+item.value+' ('+Math.round(item.value/total*100)+'%)'));chart.appendChild(segment);offset+=length;});
   chart.appendChild(element('text',{x:110,y:112,'text-anchor':'middle',fill:'#173e54','font-size':35,'font-weight':600},total.toLocaleString()));chart.appendChild(element('text',{x:110,y:135,'text-anchor':'middle',fill:'#8a9da8','font-size':10,'letter-spacing':2},'TOTAL'));visual.appendChild(chart);
  }
 }
 function start(){document.querySelectorAll('[data-hgras-chart]').forEach(render);document.querySelectorAll('[data-password-toggle]').forEach(function(button){if(button.dataset.toggleReady)return;button.dataset.toggleReady='true';button.addEventListener('click',function(){var input=document.getElementById(button.dataset.passwordToggle);if(!input)return;var visible=input.type==='password';input.type=visible?'text':'password';button.textContent=visible?'Hide':'Show';button.setAttribute('aria-pressed',String(visible));});});}
 if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);else start();
}());
