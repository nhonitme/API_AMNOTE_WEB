import{r as e,bk as a,bl as c}from"./index-Fufvmd3n.js";import{T as d}from"./tab_panel-BpoAqL51.js";/*!
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
 */const C=e.memo(e.forwardRef((t,m)=>{const n=e.useRef(null);e.useImperativeHandle(m,()=>({instance(){var o;return(o=n.current)==null?void 0:o.getInstance()}}),[]);const s=e.useMemo(()=>["items","selectedIndex","selectedItem"],[]),i=e.useMemo(()=>["onContentReady","onDisposing","onInitialized","onItemClick","onItemContextMenu","onItemHold","onItemRendered","onSelectionChanging","onTitleClick","onTitleHold","onTitleRendered"],[]),l=e.useMemo(()=>({defaultItems:"items",defaultSelectedIndex:"selectedIndex",defaultSelectedItem:"selectedItem"}),[]),r=e.useMemo(()=>({item:{optionName:"items",isCollectionItem:!0}}),[]),p=e.useMemo(()=>[{tmplOption:"itemTemplate",render:"itemRender",component:"itemComponent"},{tmplOption:"itemTitleTemplate",render:"itemTitleRender",component:"itemTitleComponent"}],[]);return e.createElement(a,{WidgetClass:d,ref:n,subscribableOptions:s,independentEvents:i,defaults:l,expectedChildren:r,templateProps:p,...t})})),u=t=>e.createElement(c,{...t,elementDescriptor:{OptionName:"items",IsCollectionItem:!0,TemplateProps:[{tmplOption:"tabTemplate",render:"tabRender",component:"tabComponent"},{tmplOption:"template",render:"render",component:"component"}]}}),b=Object.assign(u,{componentType:"option"});export{b as I,C as T};
