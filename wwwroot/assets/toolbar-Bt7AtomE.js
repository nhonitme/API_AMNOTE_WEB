import{r as e,bk as l,ey as c,bl as u}from"./index-Fufvmd3n.js";/*!
 * devextreme-react
 * Version: 25.2.5
 * Build date: Fri Feb 20 2026
 *
 * Copyright (c) 2012 - 2026 Developer Express Inc. ALL RIGHTS RESERVED
 *
 * This software may be modified and distributed under the terms
 * of the MIT license. See the LICENSE file in the root of the project for details.
 *
 * https://github.com/DevExpress/DevExtreme
 */const C=e.memo(e.forwardRef((t,m)=>{const n=e.useRef(null);e.useImperativeHandle(m,()=>({instance(){var o;return(o=n.current)==null?void 0:o.getInstance()}}),[]);const s=e.useMemo(()=>["items"],[]),r=e.useMemo(()=>["onContentReady","onDisposing","onInitialized","onItemClick","onItemContextMenu","onItemHold","onItemRendered"],[]),p=e.useMemo(()=>({defaultItems:"items"}),[]),i=e.useMemo(()=>({item:{optionName:"items",isCollectionItem:!0}}),[]),a=e.useMemo(()=>[{tmplOption:"itemTemplate",render:"itemRender",component:"itemComponent"},{tmplOption:"menuItemTemplate",render:"menuItemRender",component:"menuItemComponent"}],[]);return e.createElement(l,{WidgetClass:c,ref:n,subscribableOptions:s,independentEvents:r,defaults:p,expectedChildren:i,templateProps:a,...t})})),d=t=>e.createElement(u,{...t,elementDescriptor:{OptionName:"items",IsCollectionItem:!0,TemplateProps:[{tmplOption:"menuItemTemplate",render:"menuItemRender",component:"menuItemComponent"},{tmplOption:"template",render:"render",component:"component"}]}}),b=Object.assign(d,{componentType:"option"});export{b as I,C as T};
