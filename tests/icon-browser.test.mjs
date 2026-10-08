import assert from "node:assert/strict";
import {filterIcons,validateIconCatalog} from "../src/kernel/icon-browser.js";
const sample={schemaVersion:1,grid:24,icons:[
  {name:"workflow",category:"workflow",label:"Workflow",keywords:["diagram"],svg:"icons/workflow.svg",html:"icons/html/workflow.html"},
  {name:"search",category:"actions",label:"Search",keywords:["find"],svg:"icons/search.svg",html:"icons/html/search.html"}
]};
assert.equal(validateIconCatalog(sample).length,2);
assert.deepEqual(filterIcons(sample.icons,"DIAGRAM").map(x=>x.name),["workflow"]);
assert.deepEqual(filterIcons(sample.icons," find ").map(x=>x.name),["search"]);
assert.deepEqual(filterIcons(sample.icons,"").map(x=>x.name),["workflow","search"]);
assert.throws(()=>validateIconCatalog({...sample,grid:16}),/Unsupported/);
assert.throws(()=>validateIconCatalog({...sample,icons:[...sample.icons,sample.icons[0]]}),/duplicate/);
assert.throws(()=>validateIconCatalog({...sample,icons:[{...sample.icons[0],name:"../test"}]}),/Invalid/);
assert.throws(()=>validateIconCatalog({...sample,icons:[{...sample.icons[0],svg:"https://other.example/a.svg"}]}),/metadata/);
console.log("PASS Studio icon metadata and search adapter contract");
