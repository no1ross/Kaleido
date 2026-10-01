const pptxgen = require('pptxgenjs');
const { icon } = require('@fortawesome/fontawesome-svg-core');
const {
  faDatabase,
  faDiagramProject,
  faCompass,
  faSitemap,
  faHeadset,
  faGlobe,
  faRobot,
  faLaptopCode,
  faMagnifyingGlass,
  faListCheck,
  faBolt,
  faArrowRightLong,
  faShieldHalved,
  faArrowsRotate,
  faEye,
  faRoute,
  faPhoneVolume,
  faHospital,
  faCircleCheck,
  faLink,
  faUserGroup,
  faFileMedical,
  faPaperPlane,
  faClockRotateLeft,
  faLayerGroup,
  faChartLine,
  faGears,
  faCodeBranch,
  faCircleNodes,
  faTableList,
  faBrain,
  faMicrophoneLines,
  faWaveSquare,
  faFileContract,
} = require('@fortawesome/free-solid-svg-icons');
const fs = require('fs');

const pptx = new pptxgen();
pptx.layout = 'LAYOUT_WIDE';
pptx.author = 'Kaleido';
pptx.company = 'Kaleido';
pptx.subject = 'Executive overview of Kaleido';
pptx.title = 'Kaleido — Executive Overview';
pptx.lang = 'en-US';
pptx.theme = {
  headFontFace: 'Inter',
  bodyFontFace: 'Inter',
  lang: 'en-US'
};
pptx.defineSlideMaster({
  title: 'EXEC_DARK',
  background: { color: '080C1B' },
  objects: [
    { rect: { x: 0, y: 0, w: 13.333, h: 0.045, fill: { color: '5B5BF7' }, line: { color: '5B5BF7' } } },
  ],
  slideNumber: { x: 12.55, y: 7.09, w: 0.35, h: 0.18, color: '8490AA', fontFace: 'Inter', fontSize: 8, align: 'right' }
});
pptx.defineSlideMaster({
  title: 'EXEC_LIGHT',
  background: { color: 'F7F9FF' },
  objects: [
    { rect: { x: 0, y: 0, w: 13.333, h: 0.045, fill: { color: '5B5BF7' }, line: { color: '5B5BF7' } } },
  ],
  slideNumber: { x: 12.55, y: 7.09, w: 0.35, h: 0.18, color: '7A849B', fontFace: 'Inter', fontSize: 8, align: 'right' }
});

const W = 13.333;
const H = 7.5;
const C = {
  midnight: '080C1B',
  navy: '0C132A',
  navy2: '152247',
  ink: '111827',
  slate: '677189',
  mist: 'EEF2FF',
  white: 'FFFFFF',
  blue: '3182F6',
  indigo: '5B5BF7',
  violet: '8B5CF6',
  magenta: 'B64FD9',
  cyan: '21B6D7',
  teal: '19C4A7',
  paleBlue: 'E9F2FF',
  paleViolet: 'F0ECFF',
  paleTeal: 'E8FAF6',
  paleGray: 'F2F4F8',
  grayLine: 'D9DFEC',
  grayText: '5F6B82',
};
const ASSET = '/mnt/data/kaleido-brand/docs/assets';
const COVER = `${ASSET}/kaleido-slide-cover-16x9.png`;
const LANDING = `${ASSET}/kaleido-landing-page-concept.png`;
const LOGO_LIGHT = `${ASSET}/kaleido-logo-light.png`;
const LOGO_DARK = `${ASSET}/kaleido-logo-dark.png`;
const MARK_LIGHT = `${ASSET}/kaleido-mark-white.png`;
const MARK_COLOR = `${ASSET}/kaleido-mark-1024.png`;
const MARK_INK = `${ASSET}/kaleido-mark-ink.png`;

for (const p of [COVER, LANDING, LOGO_LIGHT, LOGO_DARK, MARK_LIGHT, MARK_COLOR, MARK_INK]) {
  if (!fs.existsSync(p)) throw new Error(`Missing asset: ${p}`);
}

const shadow = { type: 'outer', color: '0B1024', blur: 2.2, angle: 45, distance: 1.4, opacity: 0.18 };
const softShadow = { type: 'outer', color: '111827', blur: 1.6, angle: 45, distance: 1.0, opacity: 0.10 };

function svgData(iconDef, color = C.white) {
  let svg = icon(iconDef).html.join('');
  svg = svg
    .replace(/fill="currentColor"/g, `fill="#${color}"`)
    .replace(/style="[^"]*"/g, '');
  return `data:image/svg+xml;base64,${Buffer.from(svg).toString('base64')}`;
}

function addText(slide, text, x, y, w, h, opts = {}) {
  slide.addText(text, {
    x, y, w, h,
    fontFace: 'Inter',
    fontSize: opts.fontSize ?? 18,
    color: opts.color ?? C.ink,
    bold: opts.bold ?? false,
    margin: opts.margin ?? 0,
    valign: opts.valign ?? 'mid',
    breakLine: false,
    fit: 'shrink',
    align: opts.align ?? 'left',
    paraSpaceAfterPt: opts.paraSpaceAfterPt ?? 0,
    isTextBox: true,
    ...opts,
  });
}

function addRichText(slide, runs, x, y, w, h, opts = {}) {
  slide.addText(runs, {
    x, y, w, h,
    fontFace: 'Inter',
    fontSize: opts.fontSize ?? 16,
    color: opts.color ?? C.ink,
    margin: opts.margin ?? 0,
    valign: opts.valign ?? 'mid',
    fit: 'shrink',
    ...opts,
  });
}

function addPill(slide, text, x, y, w, bg, fg, opts = {}) {
  slide.addShape(pptx.ShapeType.roundRect, {
    x, y, w, h: opts.h ?? 0.33,
    rectRadius: 0.08,
    fill: { color: bg, transparency: opts.transparency ?? 0 },
    line: { color: opts.lineColor ?? bg, transparency: opts.lineTransparency ?? 100, width: 0.7 },
  });
  addText(slide, text, x, y + 0.005, w, (opts.h ?? 0.33) - 0.01, {
    fontSize: opts.fontSize ?? 9,
    color: fg,
    bold: opts.bold ?? true,
    align: 'center',
    charSpacing: opts.charSpacing ?? 0.5,
  });
}

function addIcon(slide, iconDef, x, y, size, color = C.white) {
  slide.addImage({ data: svgData(iconDef, color), x, y, w: size, h: size });
}

function addIconBadge(slide, iconDef, x, y, size, bg, iconColor = C.white, opts = {}) {
  slide.addShape(pptx.ShapeType.ellipse, {
    x, y, w: size, h: size,
    fill: { color: bg, transparency: opts.transparency ?? 0 },
    line: { color: opts.lineColor ?? bg, transparency: opts.lineTransparency ?? 100, width: 1 },
    shadow: opts.shadow ? softShadow : undefined,
  });
  const pad = size * 0.28;
  addIcon(slide, iconDef, x + pad, y + pad, size - 2 * pad, iconColor);
}

function addHeader(slide, kicker, title, subtitle, dark = false) {
  if (kicker) addText(slide, kicker.toUpperCase(), 0.73, 0.43, 4.5, 0.22, {
    fontSize: 8.5, color: dark ? '9BA7C2' : C.indigo, bold: true, charSpacing: 1.6,
  });
  addText(slide, title, 0.73, 0.70, 11.7, 0.52, {
    fontSize: 28, color: dark ? C.white : C.ink, bold: true, valign: 'top',
  });
  if (subtitle) addText(slide, subtitle, 0.73, 1.21, 11.45, 0.43, {
    fontSize: 13.5, color: dark ? 'B7C0D7' : C.grayText, valign: 'top',
  });
}

function addBrandFooter(slide, dark = false, sourceText = '') {
  slide.addShape(pptx.ShapeType.line, {
    x: 0.73, y: 7.05, w: 11.82, h: 0,
    line: { color: dark ? '2D385A' : 'D7DDEA', width: 0.6 },
  });
  slide.addImage({ path: dark ? MARK_LIGHT : MARK_INK, x: 0.73, y: 7.12, w: 0.16, h: 0.185, transparency: 15 });
  addText(slide, sourceText || 'Kaleido — executive overview', 0.98, 7.10, 9.7, 0.19, {
    fontSize: 7.5, color: dark ? '7F8DAA' : '778198',
  });
}

function addDarkDecor(slide) {
  // Subtle geometric texture and ambient glows.
  slide.addShape(pptx.ShapeType.ellipse, {
    x: 9.55, y: 0.15, w: 3.65, h: 3.65,
    fill: { color: C.violet, transparency: 87 },
    line: { color: C.violet, transparency: 100 },
  });
  slide.addShape(pptx.ShapeType.ellipse, {
    x: 10.20, y: 5.10, w: 3.00, h: 2.20,
    fill: { color: C.cyan, transparency: 92 },
    line: { color: C.cyan, transparency: 100 },
  });
  for (let i = 0; i < 9; i++) {
    slide.addShape(pptx.ShapeType.line, {
      x: 0.15 + i * 1.55, y: 0.0, w: 0, h: H,
      line: { color: '6C73A5', transparency: 94, width: 0.5 },
    });
  }
  for (let j = 0; j < 6; j++) {
    slide.addShape(pptx.ShapeType.line, {
      x: 0, y: 0.45 + j * 1.25, w: W, h: 0,
      line: { color: '6C73A5', transparency: 94, width: 0.5 },
    });
  }
}

function addLightDecor(slide) {
  slide.addShape(pptx.ShapeType.ellipse, {
    x: 9.65, y: 0.15, w: 3.55, h: 3.55,
    fill: { color: C.violet, transparency: 94 },
    line: { color: C.violet, transparency: 100 },
  });
  slide.addShape(pptx.ShapeType.ellipse, {
    x: 0.05, y: 5.35, w: 2.10, h: 1.90,
    fill: { color: C.blue, transparency: 95 },
    line: { color: C.blue, transparency: 100 },
  });
}

function addBulletRows(slide, items, x, y, w, opts = {}) {
  const rowH = opts.rowH ?? 0.42;
  items.forEach((item, i) => {
    const yy = y + i * rowH;
    slide.addShape(pptx.ShapeType.ellipse, {
      x, y: yy + 0.11, w: 0.10, h: 0.10,
      fill: { color: opts.bulletColor ?? C.indigo },
      line: { color: opts.bulletColor ?? C.indigo, transparency: 100 },
    });
    addText(slide, item, x + 0.22, yy, w - 0.22, rowH, {
      fontSize: opts.fontSize ?? 12,
      color: opts.color ?? C.grayText,
      valign: 'top',
    });
  });
}

function addRoundedCard(slide, x, y, w, h, fill, opts = {}) {
  slide.addShape(pptx.ShapeType.roundRect, {
    x, y, w, h,
    rectRadius: 0.08,
    fill: { color: fill, transparency: opts.transparency ?? 0 },
    line: { color: opts.lineColor ?? fill, transparency: opts.lineTransparency ?? 100, width: opts.lineWidth ?? 0.8 },
    shadow: opts.shadow === false ? undefined : (opts.shadow ?? softShadow),
  });
}


// Slide 1 — Cover
{
  const slide = pptx.addSlide();
  slide.background = { color: C.midnight };
  slide.addImage({ path: COVER, x: 0, y: 0, w: W, h: H });
  addPill(slide, 'EXECUTIVE OVERVIEW', 10.62, 0.38, 1.83, 'FFFFFF', C.navy, {
    h: 0.34, fontSize: 8.5, transparency: 4, lineColor: 'FFFFFF', lineTransparency: 82,
  });
  addText(slide, 'Workflows • channels • interoperability • governed AI', 0.91, 6.62, 8.5, 0.26, {
    fontSize: 12, color: 'D8DDF0', bold: false, charSpacing: 0.2,
  });
  addText(slide, 'PRE-1.0 PROJECT PREVIEW', 10.15, 6.63, 2.25, 0.23, {
    fontSize: 8.3, color: 'A9B4D1', bold: true, align: 'right', charSpacing: 1.0,
  });
}

// Slide 2 — Pain and landing concept
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  addHeader(
    slide,
    'Why leadership should care',
    'The integration tax grows with every new consumer',
    'Without a shared capability contract, each workflow, channel, standard, and agent rebuilds the same business integration.',
    true
  );

  addRoundedCard(slide, 0.73, 1.72, 4.18, 4.96, '10182F', {
    shadow: false, lineColor: '303B5B', lineTransparency: 0,
  });
  addPill(slide, 'BEFORE KALEIDO', 1.03, 2.00, 1.45, '3C2148', 'F6C8E8', {
    h: 0.31, fontSize: 7.5, lineColor: C.magenta, lineTransparency: 35, charSpacing: 0.75,
  });
  addText(slide, 'Each consumer builds its own path', 1.03, 2.42, 3.40, 0.34, {
    fontSize: 17.2, color: C.white, bold: true,
  });

  const repeated = [
    { icon: faSitemap, label: 'Workflow / Pega', color: C.violet },
    { icon: faPhoneVolume, label: 'IVR / contact center', color: C.cyan },
    { icon: faLaptopCode, label: 'Web and mobile', color: C.blue },
    { icon: faFileContract, label: 'Da Vinci / partners', color: C.teal },
    { icon: faRobot, label: 'AI agents', color: C.magenta },
  ];
  repeated.forEach((r, i) => {
    const yy = 2.96 + i * 0.48;
    addIconBadge(slide, r.icon, 1.03, yy, 0.35, '1B2947', r.color, {
      shadow: false, lineColor: '2B3A5C', lineTransparency: 0,
    });
    addText(slide, r.label, 1.52, yy - 0.01, 1.54, 0.31, {
      fontSize: 10.5, color: C.white, bold: true,
    });
    addText(slide, 'own adapter • mapping • rules', 2.92, yy - 0.01, 1.54, 0.31, {
      fontSize: 8.3, color: '929DB7', align: 'right',
    });
  });
  slide.addShape(pptx.ShapeType.line, {
    x: 1.03, y: 5.47, w: 3.58, h: 0,
    line: { color: '3A4564', width: 0.8 },
  });
  addText(slide, 'Repeated validation, state handling, errors, testing, and maintenance.', 1.03, 5.67, 3.52, 0.52, {
    fontSize: 10.5, color: 'F0C8DE', bold: true, valign: 'top',
  });
  addRoundedCard(slide, 1.03, 6.15, 3.55, 0.34, '243052', {
    shadow: false, lineColor: '445171', lineTransparency: 0,
  });
  addText(slide, 'COST AND INCONSISTENCY COMPOUND', 1.03, 6.16, 3.55, 0.31, {
    fontSize: 7.5, color: 'CBD4E8', bold: true, align: 'center', charSpacing: 0.65,
  });

  slide.addShape(pptx.ShapeType.roundRect, {
    x: 5.18, y: 1.72, w: 7.45, h: 4.96,
    rectRadius: 0.06,
    fill: { color: C.white },
    line: { color: '737EB5', transparency: 55, width: 1 },
    shadow,
  });
  slide.addImage({ path: LANDING, x: 5.18, y: 1.72, w: 7.45, h: 4.96 });
  addPill(slide, 'AFTER: ONE SHARED CAPABILITY CONTRACT', 7.85, 6.17, 4.43, C.indigo, C.white, {
    h: 0.34, fontSize: 7.7, charSpacing: 0.7, lineColor: 'A5ACFF', lineTransparency: 45,
  });

  addBrandFooter(slide, true, 'Business value: reduce duplicated integration while preserving the platforms already in place.');
}

// Slide 3 — Three business building blocks
{
  const slide = pptx.addSlide('EXEC_LIGHT');
  addLightDecor(slide);
  addHeader(
    slide,
    'What Kaleido does',
    'Three business building blocks make the model memorable',
    'The executive language describes the outcome; the Kaleido term identifies the framework primitive.',
    false
  );

  const cards = [
    {
      x: 0.73, color: C.cyan, pale: 'EAFBFE', icon: faDatabase,
      term: 'QUERYABLE', title: 'Business Information',
      question: 'What does the business know?',
      lines: ['Typed views and parameters', 'Search, filter, sort, and page', 'Published field and constraint metadata'],
      foot: 'CONSISTENT INFORMATION CONTRACTS',
    },
    {
      x: 4.55, color: C.teal, pale: 'E9FBF7', icon: faDiagramProject,
      term: 'PROCESS', title: 'Business Actions',
      question: 'What can the business do?',
      lines: ['Stateful, multi-step actions', 'Dependencies and availability rules', 'Next-step guidance through process state'],
      foot: 'CONSISTENT ACTION CONTRACTS',
    },
    {
      x: 8.37, color: C.violet, pale: 'F2EEFF', icon: faCompass,
      term: 'REGISTRY', title: 'Capability Catalog',
      question: 'How does a consumer find it?',
      lines: ['Capability names and metadata', 'Advertised query and execution URLs', 'Local and downstream aggregation'],
      foot: 'CONSISTENT DISCOVERY',
    },
  ];

  cards.forEach((c) => {
    addRoundedCard(slide, c.x, 1.90, 3.54, 4.13, C.white, {
      lineColor: 'E3E7F0', lineTransparency: 0,
    });
    slide.addShape(pptx.ShapeType.rect, {
      x: c.x, y: 1.90, w: 3.54, h: 0.08,
      fill: { color: c.color }, line: { color: c.color },
    });
    addIconBadge(slide, c.icon, c.x + 0.28, 2.22, 0.66, c.pale, c.color, {
      shadow: false, lineColor: c.pale,
    });
    addPill(slide, c.term, c.x + 1.08, 2.24, 1.15, c.pale, c.color, {
      h: 0.29, fontSize: 7.4, charSpacing: 0.85,
    });
    addText(slide, c.title, c.x + 0.28, 2.83, 2.96, 0.50, {
      fontSize: 19.5, color: C.ink, bold: true, valign: 'top',
    });
    addText(slide, c.question, c.x + 0.28, 3.42, 2.96, 0.40, {
      fontSize: 12.8, color: C.ink, bold: true, valign: 'top',
    });
    addBulletRows(slide, c.lines, c.x + 0.31, 4.00, 2.96, {
      rowH: 0.47, fontSize: 10.9, bulletColor: c.color, color: C.grayText,
    });
    addPill(slide, c.foot, c.x + 0.28, 5.55, 2.42, c.pale, c.color, {
      h: 0.31, fontSize: 6.8, charSpacing: 0.5,
    });
  });

  addRoundedCard(slide, 0.73, 6.28, 11.70, 0.49, C.navy, {
    shadow: false, lineTransparency: 100,
  });
  addRichText(slide, [
    { text: 'BUSINESS INFORMATION', options: { bold: true, color: C.cyan } },
    { text: '   +   ', options: { bold: true, color: '8B96AE' } },
    { text: 'BUSINESS ACTIONS', options: { bold: true, color: C.teal } },
    { text: '   +   ', options: { bold: true, color: '8B96AE' } },
    { text: 'CAPABILITY CATALOG', options: { bold: true, color: C.violet } },
    { text: '   =   ', options: { bold: true, color: '8B96AE' } },
    { text: 'A SHARED OPERATING MODEL', options: { bold: true, color: C.white } },
  ], 1.03, 6.37, 10.97, 0.26, { fontSize: 10.5, align: 'center' });
  addBrandFooter(slide, false);
}

// Slide 4 — Shared capability layer
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  addHeader(
    slide,
    'Operating model',
    'Build once. Reuse everywhere.',
    'One capability model across every channel—without replacing workflow, channel, or interoperability platforms.',
    true
  );
  addPill(slide, 'SHARED CAPABILITY CONTRACT', 9.77, 0.53, 2.55, C.indigo, C.white, {
    h: 0.31, fontSize: 7.4, charSpacing: 0.8,
  });

  const consumers = [
    { x: 0.75, icon: faSitemap, label: 'Workflow &\ncase tools', color: C.violet },
    { x: 3.19, icon: faHeadset, label: 'IVR / contact\ncenter', color: C.cyan },
    { x: 5.63, icon: faLaptopCode, label: 'Digital\nchannels', color: C.blue },
    { x: 8.07, icon: faFileContract, label: 'Standards &\npartners', color: C.teal },
    { x: 10.51, icon: faRobot, label: 'Governed AI\nagents', color: C.magenta },
  ];
  consumers.forEach((c) => {
    addRoundedCard(slide, c.x, 1.83, 2.07, 1.10, '111A35', {
      shadow: false, lineColor: '2C385B', lineTransparency: 0,
    });
    addIconBadge(slide, c.icon, c.x + 0.18, 2.02, 0.58, c.color, C.white, { shadow: false });
    addText(slide, c.label, c.x + 0.86, 1.94, 1.03, 0.70, {
      fontSize: 11.7, color: C.white, bold: true, valign: 'mid', breakLine: true,
    });
    slide.addShape(pptx.ShapeType.line, {
      x: c.x + 1.04, y: 2.95, w: 0, h: 0.55,
      line: { color: c.color, width: 1.5, endArrowType: 'triangle' },
    });
  });

  addRoundedCard(slide, 1.14, 3.46, 11.05, 1.55, C.white, {
    lineColor: 'FFFFFF', lineTransparency: 84, shadow,
  });
  slide.addImage({ path: MARK_COLOR, x: 1.43, y: 3.71, w: 0.92, h: 0.92, transparency: 3 });
  addText(slide, 'KALEIDO', 2.55, 3.57, 2.18, 0.38, {
    fontSize: 19, color: C.ink, bold: true,
  });
  addText(slide, 'Shared business capability contract', 2.55, 3.93, 2.72, 0.28, {
    fontSize: 10.9, color: C.grayText,
  });
  const modules = [
    { x: 5.22, name: 'BUSINESS INFORMATION', term: 'Queryable', color: C.cyan },
    { x: 7.42, name: 'BUSINESS ACTIONS', term: 'Process', color: C.teal },
    { x: 9.62, name: 'CAPABILITY CATALOG', term: 'Registry', color: C.violet },
  ];
  modules.forEach((m) => {
    addRoundedCard(slide, m.x, 3.70, 1.93, 0.91, 'F7F9FF', {
      lineColor: 'E1E5EF', lineTransparency: 0, shadow: false,
    });
    slide.addShape(pptx.ShapeType.rect, {
      x: m.x, y: 3.70, w: 0.08, h: 0.91,
      fill: { color: m.color }, line: { color: m.color },
    });
    addText(slide, m.name, m.x + 0.18, 3.80, 1.58, 0.28, {
      fontSize: 7.5, color: C.ink, bold: true, charSpacing: 0.25,
    });
    addText(slide, m.term, m.x + 0.18, 4.15, 1.58, 0.22, {
      fontSize: 9.2, color: m.color, bold: true,
    });
  });
  addText(slide, 'metadata • validation • typed clients • process identity • correlation • telemetry', 2.55, 4.49, 7.90, 0.24, {
    fontSize: 9.2, color: '626D83',
  });

  const business = [
    { x: 0.88, label: 'Member', icon: faUserGroup },
    { x: 2.90, label: 'Provider', icon: faHospital },
    { x: 4.92, label: 'Rules', icon: faListCheck },
    { x: 6.94, label: 'Orders', icon: faTableList },
    { x: 8.96, label: 'Prior Auth', icon: faFileMedical },
    { x: 10.98, label: 'Reference', icon: faDatabase },
  ];
  business.forEach((b, i) => {
    slide.addShape(pptx.ShapeType.line, {
      x: b.x + 0.73, y: 5.02, w: 0, h: 0.50,
      line: { color: i % 2 ? C.teal : C.blue, width: 1.25, endArrowType: 'triangle' },
    });
    addRoundedCard(slide, b.x, 5.56, 1.60, 0.72, '111A35', {
      shadow: false, lineColor: '303B5B', lineTransparency: 0,
    });
    addIcon(slide, b.icon, b.x + 0.15, 5.76, 0.25, i % 2 ? C.teal : C.cyan);
    addText(slide, b.label, b.x + 0.48, 5.70, 0.96, 0.31, {
      fontSize: 10.5, color: C.white, bold: true,
    });
  });

  addText(slide, 'Kaleido is the contract layer—not the workflow engine, channel runtime, or interoperability standard.', 0.74, 6.54, 9.65, 0.31, {
    fontSize: 11.2, color: 'C8D1E4', bold: true,
  });
  addText(slide, 'The platforms keep doing what they do best.', 9.64, 6.54, 2.64, 0.31, {
    fontSize: 9.6, color: '8E9AB5', align: 'right',
  });
  addBrandFooter(slide, true);
}

// Slide 5 — Current target consumers
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  addHeader(
    slide,
    'Current direction',
    'Where Kaleido fits first',
    'The immediate value is reuse across orchestration, voice channels, and standards-based integrations. AI follows from the same foundation.',
    true
  );
  addPill(slide, 'INITIAL ADOPTION PATHS', 10.37, 0.53, 1.91, C.teal, C.navy, {
    h: 0.31, fontSize: 7.2, charSpacing: 0.8,
  });

  const useCases = [
    {
      x: 0.73, color: C.violet, icon: faSitemap,
      eyebrow: 'WORKFLOW & CASE MANAGEMENT',
      title: 'Pega and similar platforms',
      body: 'Keep orchestration, SLAs, assignments, decisions, and human work in the workflow platform. Call Kaleido for reusable business information and actions.',
      tags: ['Shared contracts', 'Less custom integration'],
      note: 'Workflow platforms coordinate the work; Kaleido standardizes the business capabilities they call.'
    },
    {
      x: 4.55, color: C.cyan, icon: faPhoneVolume,
      eyebrow: 'IVR / CONTACT CENTER',
      title: 'Voice and assisted channels',
      body: 'Use capability metadata to drive prompts and validation, preserve a process ID, and resume the same journey across transfers, agents, web, or mobile.',
      tags: ['Consistent prompts', 'Channel continuity'],
      note: 'One business action can support voice, agent desktop, and digital self-service.'
    },
    {
      x: 8.37, color: C.teal, icon: faFileContract,
      eyebrow: 'STANDARDS & EXTERNAL PARTNERS',
      title: 'Da Vinci CRD · DTR · PAS',
      body: 'Handle FHIR- and X12-oriented exchange at the boundary, then map it into stable internal information and action contracts that other consumers can reuse.',
      tags: ['Standards at edge', 'Stable internal model'],
      note: 'CRD surfaces requirements, DTR supports documentation capture, and PAS supports prior-authorization exchange.'
    },
  ];

  useCases.forEach((u) => {
    addRoundedCard(slide, u.x, 1.87, 3.55, 4.55, '10182F', {
      shadow: false, lineColor: '2E3858', lineTransparency: 0,
    });
    slide.addShape(pptx.ShapeType.rect, {
      x: u.x, y: 1.87, w: 3.55, h: 0.09,
      fill: { color: u.color }, line: { color: u.color },
    });
    addIconBadge(slide, u.icon, u.x + 0.29, 2.22, 0.70, u.color, C.white, { shadow: false });
    addText(slide, u.eyebrow, u.x + 1.16, 2.20, 2.08, 0.30, {
      fontSize: 7.2, color: '9EAAC4', bold: true, charSpacing: 0.75,
    });
    addText(slide, u.title, u.x + 1.16, 2.49, 2.08, 0.56, {
      fontSize: 16.1, color: C.white, bold: true, valign: 'top',
    });
    addText(slide, u.body, u.x + 0.29, 3.30, 2.92, 1.26, {
      fontSize: 10.8, color: 'C3CBDE', valign: 'top',
    });
    addPill(slide, u.tags[0].toUpperCase(), u.x + 0.29, 4.75, 1.48, '172540', u.color, {
      h: 0.30, fontSize: 6.7, lineColor: u.color, lineTransparency: 35, charSpacing: 0.35,
    });
    addPill(slide, u.tags[1].toUpperCase(), u.x + 1.85, 4.75, 1.38, '172540', u.color, {
      h: 0.30, fontSize: 6.3, lineColor: u.color, lineTransparency: 35, charSpacing: 0.2,
    });
    slide.addShape(pptx.ShapeType.line, {
      x: u.x + 0.29, y: 5.30, w: 2.94, h: 0,
      line: { color: '37425F', width: 0.8 },
    });
    addText(slide, u.note, u.x + 0.29, 5.46, 2.94, 0.70, {
      fontSize: 8.8, color: '8F9BB7', italic: true, valign: 'top',
    });
  });

  addRoundedCard(slide, 0.73, 6.58, 11.70, 0.31, '18223D', {
    shadow: false, lineColor: '33405F', lineTransparency: 0,
  });
  addRichText(slide, [
    { text: 'CROSS-CUTTING RESULT: ', options: { bold: true, color: C.white } },
    { text: 'digital experiences and AI agents consume the same catalog rather than introducing another bespoke integration path.', options: { color: 'AEB8CF' } },
  ], 1.02, 6.60, 10.95, 0.25, { fontSize: 9.0, align: 'center' });

  addRichText(slide, [
    { text: 'Official references: ', options: { color: '7786A7' } },
    { text: 'Pega workflow automation', options: { color: '9DB3FF', hyperlink: { url: 'https://www.pega.com/products/platform/workflow-automation' } } },
    { text: ' • ', options: { color: '7786A7' } },
    { text: 'HL7 Da Vinci CRD', options: { color: '9DB3FF', hyperlink: { url: 'https://www.hl7.org/fhir/us/davinci-crd/' } } },
    { text: ' • ', options: { color: '7786A7' } },
    { text: 'DTR', options: { color: '9DB3FF', hyperlink: { url: 'https://www.hl7.org/fhir/us/davinci-dtr/' } } },
    { text: ' • ', options: { color: '7786A7' } },
    { text: 'PAS', options: { color: '9DB3FF', hyperlink: { url: 'https://www.hl7.org/fhir/us/davinci-pas/' } } },
  ], 4.95, 7.10, 7.30, 0.19, { fontSize: 6.8, align: 'right' });
  addBrandFooter(slide, true);
}

// Slide 6 — AI as a governed consumer
{
  const slide = pptx.addSlide('EXEC_LIGHT');
  addLightDecor(slide);
  addHeader(
    slide,
    'AI as a consequence',
    'AI becomes another governed consumer',
    'Typed, discoverable capabilities let an agent use the same business contracts as workflow tools and channels—not unrestricted authority.',
    false
  );
  addPill(slide, 'NOT UNRESTRICTED AUTHORITY', 9.49, 0.53, 2.84, 'F1EDFF', C.violet, {
    h: 0.31, fontSize: 7.5, charSpacing: 0.8, lineColor: 'D9D1FF', lineTransparency: 0,
  });

  const flow = [
    { icon: faMagnifyingGlass, title: 'Discover', line: 'Read the catalog', color: C.blue },
    { icon: faBrain, title: 'Understand', line: 'Schemas and constraints', color: C.indigo },
    { icon: faCircleCheck, title: 'Choose', line: 'Select an allowed capability', color: C.violet },
    { icon: faBolt, title: 'Execute', line: 'Call the advertised route', color: C.magenta },
    { icon: faRoute, title: 'Continue', line: 'Use processId and next steps', color: C.teal },
  ];
  const startX = 0.88;
  const gap = 2.42;
  slide.addShape(pptx.ShapeType.line, {
    x: 1.46, y: 2.62, w: 9.66, h: 0,
    line: { color: 'C9D2E7', width: 3.0, beginArrowType: 'none', endArrowType: 'triangle' },
  });
  flow.forEach((f, i) => {
    const x = startX + i * gap;
    addIconBadge(slide, f.icon, x, 2.13, 0.98, f.color, C.white, { shadow: true });
    addText(slide, f.title, x - 0.23, 3.26, 1.45, 0.32, {
      fontSize: 13.2, color: C.ink, bold: true, align: 'center',
    });
    addText(slide, f.line, x - 0.40, 3.61, 1.78, 0.47, {
      fontSize: 9.2, color: C.grayText, align: 'center', valign: 'top',
    });
  });

  addRoundedCard(slide, 0.74, 4.35, 5.93, 2.18, C.navy, { shadow });
  addPill(slide, 'THE SHARED CATALOG ENABLES', 1.03, 4.64, 2.14, C.blue, C.white, {
    h: 0.31, fontSize: 7.2, charSpacing: 0.65,
  });
  addText(slide, 'Machine-readable operating context', 1.03, 5.02, 4.78, 0.34, {
    fontSize: 16.5, color: C.white, bold: true,
  });
  addBulletRows(slide, [
    'Capability names, descriptions, schemas, and constraints',
    'Advertised query and execution routes',
    'Explicit process identity and next-step guidance',
    'Structured results, errors, and correlation context',
  ], 1.06, 5.44, 5.14, {
    rowH: 0.30, fontSize: 9.6, bulletColor: C.cyan, color: 'CFD7E9',
  });

  addRoundedCard(slide, 6.92, 4.35, 5.66, 2.18, 'F1EDFF', {
    lineColor: 'DED5FF', lineTransparency: 0, shadow,
  });
  addPill(slide, 'ENTERPRISE GOVERNANCE ADDS', 7.20, 4.64, 2.20, C.violet, C.white, {
    h: 0.31, fontSize: 7.2, charSpacing: 0.65,
  });
  addText(slide, 'A controlled decision boundary', 7.20, 5.02, 4.55, 0.34, {
    fontSize: 16.5, color: C.ink, bold: true,
  });
  addBulletRows(slide, [
    'Authorization-aware discovery and allowlists',
    'Confirmation and side-effect classifications',
    'Durable idempotency and concurrency guarantees',
    'Registry freshness, redaction, and resource limits',
  ], 7.22, 5.44, 4.98, {
    rowH: 0.30, fontSize: 9.6, bulletColor: C.violet, color: C.grayText,
  });

  addBrandFooter(slide, false, 'AI readiness is a consequence of a good capability architecture—not the primary reason to build it.');
}

// Slide 7 — Prior authorization proof point
{
  const slide = pptx.addSlide('EXEC_LIGHT');
  addLightDecor(slide);
  addHeader(
    slide,
    'Prior authorization proof point',
    'One process. Multiple channels. Same business rules.',
    'The PriorAuth sample makes the operating model tangible: consumers change, while the capability contracts and process state remain consistent.',
    false
  );
  addPill(slide, 'ILLUSTRATIVE PRIOR AUTH JOURNEY', 9.58, 0.53, 2.76, 'EDE9FE', C.violet, {
    h: 0.31, fontSize: 7.1, charSpacing: 0.65,
  });

  addRoundedCard(slide, 0.73, 1.86, 2.02, 4.66, C.navy, { shadow, lineTransparency: 100 });
  addText(slide, 'CONSUMERS', 1.03, 2.14, 1.42, 0.22, {
    fontSize: 8.3, color: '9DA8C3', bold: true, charSpacing: 1.1,
  });
  const chan = [
    { icon: faSitemap, label: 'Pega', color: C.violet },
    { icon: faPhoneVolume, label: 'IVR', color: C.cyan },
    { icon: faLaptopCode, label: 'Web / app', color: C.blue },
    { icon: faGlobe, label: 'Da Vinci', color: C.teal },
    { icon: faRobot, label: 'AI agent', color: C.magenta },
  ];
  chan.forEach((c, i) => {
    const yy = 2.59 + i * 0.68;
    addIconBadge(slide, c.icon, 1.03, yy, 0.40, '1B2947', c.color, {
      shadow: false, lineColor: '2B3A5C', lineTransparency: 0,
    });
    addText(slide, c.label, 1.58, yy + 0.02, 0.94, 0.31, {
      fontSize: 11.0, color: C.white, bold: true,
    });
  });
  addText(slide, 'Same business\ncapabilities', 1.03, 6.02, 1.30, 0.38, {
    fontSize: 9.8, color: 'AEB8CF', bold: true, breakLine: true, valign: 'top',
  });

  addRoundedCard(slide, 3.02, 1.86, 7.23, 4.66, C.white, {
    lineColor: 'E0E5F0', lineTransparency: 0, shadow,
  });
  addText(slide, 'PROCESS THREAD', 3.34, 2.14, 2.1, 0.22, {
    fontSize: 8.3, color: C.indigo, bold: true, charSpacing: 1.1,
  });
  addPill(slide, 'processId', 8.98, 2.05, 0.86, 'EEF2FF', C.indigo, {
    h: 0.31, fontSize: 7.1, lineColor: 'C8D1EE', lineTransparency: 0, charSpacing: 0.4,
  });
  slide.addShape(pptx.ShapeType.line, {
    x: 3.78, y: 4.03, w: 5.92, h: 0,
    line: { color: 'AAB4CD', width: 2.1, endArrowType: 'triangle' },
  });

  const journey = [
    { x: 3.39, icon: faCircleNodes, label: 'Start\nintake', service: 'intake', color: C.indigo },
    { x: 4.53, icon: faUserGroup, label: 'Find\nmember', service: 'member', color: C.blue },
    { x: 5.67, icon: faFileMedical, label: 'Capture\nservice', service: 'radiology', color: C.cyan },
    { x: 6.81, icon: faHospital, label: 'Select\nprovider', service: 'provider', color: C.teal },
    { x: 7.95, icon: faListCheck, label: 'Complete\nquestions', service: 'configuration', color: C.violet },
    { x: 9.09, icon: faPaperPlane, label: 'Submit &\nrecord', service: 'history', color: C.magenta },
  ];
  journey.forEach((j, i) => {
    addIconBadge(slide, j.icon, j.x, 3.54, 0.80, j.color, C.white, { shadow: true });
    addText(slide, j.label, j.x - 0.18, 4.58, 1.16, 0.56, {
      fontSize: 10.2, color: C.ink, bold: true, align: 'center', valign: 'top', breakLine: true,
    });
    addPill(slide, j.service, j.x - 0.10, 5.28, 1.02, 'F2F4F8', C.grayText, {
      h: 0.29, fontSize: 7.0, bold: true, lineColor: 'DCE1EB', lineTransparency: 0,
    });
    if (i < journey.length - 1) {
      addIcon(slide, faArrowRightLong, j.x + 0.86, 3.86, 0.22, '8793AE');
    }
  });
  addText(slide, 'Business Information supplies context; Business Actions manage the resumable journey.', 3.34, 5.88, 6.52, 0.39, {
    fontSize: 10.2, color: C.grayText, italic: true, align: 'center',
  });

  addRoundedCard(slide, 10.52, 1.86, 2.06, 4.66, 'F1EDFF', {
    lineColor: 'DED6FF', lineTransparency: 0, shadow,
  });
  addText(slide, 'SHARED OUTCOME', 10.81, 2.14, 1.48, 0.22, {
    fontSize: 8.2, color: C.violet, bold: true, charSpacing: 0.9,
  });
  const outcomes = [
    ['Same metadata', faCompass],
    ['Same validation', faCircleCheck],
    ['Same process state', faClockRotateLeft],
    ['Same correlation', faLink],
    ['Same observability', faEye],
  ];
  outcomes.forEach((o, i) => {
    const yy = 2.65 + i * 0.66;
    addIcon(slide, o[1], 10.83, yy + 0.02, 0.24, C.violet);
    addText(slide, o[0], 11.20, yy, 1.05, 0.31, {
      fontSize: 10.1, color: C.ink, bold: true,
    });
  });
  addText(slide, 'The channel can change without redefining the business capability.', 10.81, 5.94, 1.49, 0.42, {
    fontSize: 9.0, color: C.grayText, italic: true, valign: 'top',
  });

  addBrandFooter(slide, false, 'Illustrative: service and workflow concepts are grounded in the repository’s PriorAuth sample.');
}

// Slide 8 — Executive takeaway
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  slide.addImage({ path: MARK_LIGHT, x: 10.06, y: 0.60, w: 2.73, h: 3.15, transparency: 84 });
  addText(slide, 'EXECUTIVE TAKEAWAY', 0.74, 0.61, 4.3, 0.22, {
    fontSize: 8.5, color: '9BA7C2', bold: true, charSpacing: 1.6,
  });
  addText(slide, 'Standardize once.\nReuse everywhere.', 0.74, 1.00, 7.76, 1.28, {
    fontSize: 34, color: C.white, bold: true, valign: 'top', breakLine: true,
  });

  addRoundedCard(slide, 0.74, 2.53, 11.84, 1.63, '111A35', {
    shadow: false, lineColor: '354362', lineTransparency: 0,
  });
  slide.addShape(pptx.ShapeType.rect, {
    x: 0.74, y: 2.53, w: 0.10, h: 1.63,
    fill: { color: C.violet }, line: { color: C.violet },
  });
  addText(slide,
    'Kaleido is not another workflow engine, channel platform, or interoperability layer.',
    1.13, 2.78, 10.73, 0.36,
    { fontSize: 18.5, color: C.white, bold: true }
  );
  addText(slide,
    'It is the shared capability contract that allows all of them to work together.',
    1.13, 3.29, 10.73, 0.37,
    { fontSize: 18.5, color: 'BFC8DC', bold: true }
  );

  const outcomes = [
    {
      x: 0.74, color: C.cyan, icon: faLayerGroup,
      title: 'One capability model',
      body: 'Business information, actions, metadata, validation, and state are defined consistently.',
    },
    {
      x: 4.48, color: C.violet, icon: faCodeBranch,
      title: 'Many consumers',
      body: 'Pega, IVR, digital channels, Da Vinci integrations, partners, and agents reuse the same contract.',
    },
    {
      x: 8.22, color: C.teal, icon: faCircleCheck,
      title: 'Consistent behavior',
      body: 'Channels can change without duplicating the business rules or redefining the journey.',
    },
  ];
  outcomes.forEach((o) => {
    addRoundedCard(slide, o.x, 4.55, 3.47, 1.48, '10182F', {
      shadow: false, lineColor: '303B5B', lineTransparency: 0,
    });
    addIconBadge(slide, o.icon, o.x + 0.22, 4.82, 0.58, o.color, C.white, { shadow: false });
    addText(slide, o.title, o.x + 0.95, 4.72, 2.14, 0.42, {
      fontSize: 14.3, color: C.white, bold: true,
    });
    addText(slide, o.body, o.x + 0.22, 5.34, 2.98, 0.48, {
      fontSize: 9.6, color: 'AEB8CD', valign: 'top',
    });
  });

  addPill(slide, 'DEFINE BUSINESS CAPABILITIES ONCE', 0.74, 6.44, 3.20, C.indigo, C.white, {
    h: 0.34, fontSize: 7.6, charSpacing: 0.75,
  });
  addText(slide, 'Then allow every authorized consumer to discover and use them consistently.', 4.22, 6.43, 7.75, 0.35, {
    fontSize: 13.0, color: 'D1D8E7', bold: true,
  });
  addText(slide, 'PRE-1.0 PROJECT PREVIEW', 10.15, 6.88, 2.20, 0.18, {
    fontSize: 7.4, color: '7F8DAA', bold: true, align: 'right', charSpacing: 0.9,
  });
  addBrandFooter(slide, true, 'Kaleido — typed, discoverable, stateful business capabilities.');
}

pptx.writeFile({ fileName: '/mnt/data/Kaleido_Executive_Overview_v2.pptx' });
