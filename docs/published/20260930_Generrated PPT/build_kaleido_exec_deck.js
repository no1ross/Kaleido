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
  addText(slide, 'Workflows • channels • interoperability • AI', 0.91, 6.62, 7.8, 0.26, {
    fontSize: 12, color: 'D8DDF0', bold: false, charSpacing: 0.2,
  });
  addText(slide, 'PRE-1.0 PROJECT PREVIEW', 10.15, 6.63, 2.25, 0.23, {
    fontSize: 8.3, color: 'A9B4D1', bold: true, align: 'right', charSpacing: 1.0,
  });
}

// Slide 2 — Landing concept visual
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  slide.addShape(pptx.ShapeType.roundRect, {
    x: 3.17, y: 0.37, w: 9.82, h: 6.55,
    rectRadius: 0.06,
    fill: { color: C.white },
    line: { color: '737EB5', transparency: 55, width: 1 },
    shadow,
  });
  slide.addImage({ path: LANDING, x: 3.17, y: 0.37, w: 9.82, h: 6.55 });
  addText(slide, 'THE IDEA\nAT A GLANCE', 0.72, 0.78, 2.05, 1.12, {
    fontSize: 24, color: C.white, bold: true, valign: 'top', breakLine: true,
  });
  addText(slide, 'One contract layer for humans, applications, and AI.', 0.72, 1.98, 2.06, 0.84, {
    fontSize: 13.5, color: 'B8C2D9', valign: 'top',
  });

  const steps = [
    ['01', 'Discover', 'What capabilities exist?'],
    ['02', 'Invoke', 'How do I call them?'],
    ['03', 'Continue', 'What comes next?'],
  ];
  steps.forEach((s, i) => {
    const yy = 3.18 + i * 0.91;
    addPill(slide, s[0], 0.72, yy, 0.43, i === 0 ? C.blue : i === 1 ? C.violet : C.teal, C.white, { h: 0.32, fontSize: 8 });
    addText(slide, s[1], 1.31, yy - 0.03, 1.34, 0.28, { fontSize: 14, color: C.white, bold: true });
    addText(slide, s[2], 1.31, yy + 0.27, 1.47, 0.36, { fontSize: 10.2, color: '9DA9C4', valign: 'top' });
  });
  addText(slide, 'One business capability model.\nMany consumers.', 0.72, 6.05, 2.05, 0.66, {
    fontSize: 13.2, color: 'D8DFF0', bold: true, valign: 'top', breakLine: true,
  });
}

// Slide 3 — What it does
{
  const slide = pptx.addSlide('EXEC_LIGHT');
  addLightDecor(slide);
  addHeader(slide, 'What Kaleido does', 'Three primitives make the system understandable', 'Kaleido turns ordinary .NET types into consistent, discoverable runtime contracts.', false);

  const cards = [
    {
      x: 0.73, color: C.cyan, pale: 'EAFBFE', icon: faDatabase,
      title: 'Queryable', question: 'What does the business know?',
      lines: ['Typed views and parameters', 'Search, filter, sort, and page', 'Published field and constraint metadata'],
      foot: 'Information capabilities',
    },
    {
      x: 4.55, color: C.teal, pale: 'E9FBF7', icon: faDiagramProject,
      title: 'Process', question: 'What can the business do?',
      lines: ['Stateful, multi-step actions', 'Dependencies and availability rules', 'Next-step guidance through process state'],
      foot: 'Action capabilities',
    },
    {
      x: 8.37, color: C.violet, pale: 'F2EEFF', icon: faCompass,
      title: 'Registry', question: 'How does a consumer find it?',
      lines: ['Capability catalog and metadata', 'Advertised execute and query URLs', 'Local and downstream aggregation'],
      foot: 'Discovery capabilities',
    },
  ];

  cards.forEach((c) => {
    addRoundedCard(slide, c.x, 1.92, 3.54, 3.94, C.white, { lineColor: 'E3E7F0', lineTransparency: 0 });
    slide.addShape(pptx.ShapeType.rect, { x: c.x, y: 1.92, w: 3.54, h: 0.08, fill: { color: c.color }, line: { color: c.color } });
    addIconBadge(slide, c.icon, c.x + 0.28, 2.23, 0.66, c.pale, c.color, { shadow: false, lineColor: c.pale });
    addText(slide, c.title, c.x + 1.08, 2.18, 2.08, 0.35, { fontSize: 20, color: C.ink, bold: true });
    addText(slide, c.question, c.x + 0.28, 2.86, 2.96, 0.52, { fontSize: 14, color: C.ink, bold: true, valign: 'top' });
    addBulletRows(slide, c.lines, c.x + 0.31, 3.54, 2.96, { rowH: 0.52, fontSize: 11.4, bulletColor: c.color, color: C.grayText });
    addPill(slide, c.foot.toUpperCase(), c.x + 0.28, 5.25, 1.78, c.pale, c.color, { h: 0.32, fontSize: 7.7, charSpacing: 0.6 });
  });

  addRoundedCard(slide, 0.73, 6.13, 11.7, 0.63, C.navy, { shadow: false, lineTransparency: 100 });
  addRichText(slide, [
    { text: 'ORDINARY .NET TYPES', options: { bold: true, color: C.white } },
    { text: '   →   ', options: { bold: true, color: C.cyan } },
    { text: 'VALIDATED METADATA', options: { bold: true, color: C.white } },
    { text: '   →   ', options: { bold: true, color: C.violet } },
    { text: 'CONSISTENT HTTP CAPABILITIES', options: { bold: true, color: C.white } },
  ], 1.03, 6.23, 8.5, 0.30, { fontSize: 11.7, valign: 'mid' });
  addText(slide, 'Correlation • clients • observability • persistence', 9.16, 6.22, 2.94, 0.30, { fontSize: 9.2, color: 'AEB9D1', align: 'right' });
  addBrandFooter(slide, false);
}

// Slide 4 — Shared capability layer
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  addHeader(slide, 'Operating model', 'A shared capability layer—without replacing your platforms', 'Define business capabilities once, then let workflow tools, channels, partners, and agents consume the same contracts.', true);
  addPill(slide, 'DEFINE ONCE • REUSE EVERYWHERE', 9.86, 0.52, 2.48, C.indigo, C.white, { h: 0.31, fontSize: 7.5, charSpacing: 0.8 });

  const consumers = [
    { x: 0.75, icon: faSitemap, label: 'Workflow\nengines', color: C.violet },
    { x: 3.19, icon: faHeadset, label: 'IVR / contact\ncenter', color: C.cyan },
    { x: 5.63, icon: faLaptopCode, label: 'Digital\nchannels', color: C.blue },
    { x: 8.07, icon: faGlobe, label: 'Partner\nintegrations', color: C.teal },
    { x: 10.51, icon: faRobot, label: 'AI\nagents', color: C.magenta },
  ];
  consumers.forEach((c) => {
    addRoundedCard(slide, c.x, 1.83, 2.07, 1.10, '111A35', { shadow: false, lineColor: '2C385B', lineTransparency: 0 });
    addIconBadge(slide, c.icon, c.x + 0.18, 2.02, 0.58, c.color, C.white, { shadow: false });
    addText(slide, c.label, c.x + 0.86, 1.94, 1.03, 0.70, { fontSize: 12.2, color: C.white, bold: true, valign: 'mid', breakLine: true });
    slide.addShape(pptx.ShapeType.line, { x: c.x + 1.04, y: 2.95, w: 0, h: 0.55, line: { color: c.color, width: 1.5, endArrowType: 'triangle' } });
  });

  addRoundedCard(slide, 1.14, 3.46, 11.05, 1.55, C.white, { lineColor: 'FFFFFF', lineTransparency: 84, shadow });
  slide.addImage({ path: MARK_COLOR, x: 1.43, y: 3.71, w: 0.92, h: 0.92, transparency: 3 });
  addText(slide, 'KALEIDO', 2.55, 3.57, 2.18, 0.38, { fontSize: 19, color: C.ink, bold: true });
  addText(slide, 'Typed business capability layer', 2.55, 3.93, 2.54, 0.28, { fontSize: 11.1, color: C.grayText });
  const modules = [
    { x: 5.26, name: 'QUERYABLE', color: C.cyan, line: 'information' },
    { x: 7.42, name: 'PROCESS', color: C.teal, line: 'actions + state' },
    { x: 9.58, name: 'REGISTRY', color: C.violet, line: 'metadata + routes' },
  ];
  modules.forEach((m) => {
    addRoundedCard(slide, m.x, 3.73, 1.89, 0.86, 'F7F9FF', { lineColor: 'E1E5EF', lineTransparency: 0, shadow: false });
    slide.addShape(pptx.ShapeType.rect, { x: m.x, y: 3.73, w: 0.08, h: 0.86, fill: { color: m.color }, line: { color: m.color } });
    addText(slide, m.name, m.x + 0.18, 3.85, 1.55, 0.20, { fontSize: 9.5, color: C.ink, bold: true, charSpacing: 0.6 });
    addText(slide, m.line, m.x + 0.18, 4.11, 1.55, 0.22, { fontSize: 8.8, color: C.grayText });
  });
  addText(slide, 'validation • typed clients • process identifiers • correlation • telemetry', 2.55, 4.48, 7.74, 0.26, { fontSize: 9.4, color: '626D83' });

  const business = [
    { x: 0.88, label: 'Member', icon: faUserGroup },
    { x: 2.90, label: 'Provider', icon: faHospital },
    { x: 4.92, label: 'Rules', icon: faListCheck },
    { x: 6.94, label: 'Orders', icon: faTableList },
    { x: 8.96, label: 'Prior Auth', icon: faFileMedical },
    { x: 10.98, label: 'Reference', icon: faDatabase },
  ];
  business.forEach((b, i) => {
    slide.addShape(pptx.ShapeType.line, { x: b.x + 0.73, y: 5.02, w: 0, h: 0.50, line: { color: i % 2 ? C.teal : C.blue, width: 1.25, endArrowType: 'triangle' } });
    addRoundedCard(slide, b.x, 5.56, 1.60, 0.72, '111A35', { shadow: false, lineColor: '303B5B', lineTransparency: 0 });
    addIcon(slide, b.icon, b.x + 0.15, 5.76, 0.25, i % 2 ? C.teal : C.cyan);
    addText(slide, b.label, b.x + 0.48, 5.70, 0.96, 0.31, { fontSize: 10.5, color: C.white, bold: true, align: 'left' });
  });

  addText(slide, 'Kaleido complements orchestration platforms, channel runtimes, and interoperability standards.', 0.74, 6.55, 8.85, 0.30, { fontSize: 11.5, color: 'B8C3DA', bold: true });
  addText(slide, 'The value is the shared contract between them.', 9.21, 6.55, 3.08, 0.30, { fontSize: 10.5, color: '8E9AB5', align: 'right' });
  addBrandFooter(slide, true);
}

// Slide 5 — AI agent use
{
  const slide = pptx.addSlide('EXEC_LIGHT');
  addLightDecor(slide);
  addHeader(slide, 'AI enablement', 'What an AI agent can do with Kaleido', 'Kaleido gives an agent a map of business capabilities—not unrestricted authority.', false);
  addPill(slide, 'DISCOVER → UNDERSTAND → ACT → CONTINUE', 8.65, 0.53, 3.70, 'EDE9FE', C.violet, { h: 0.31, fontSize: 7.6, charSpacing: 0.7 });

  const flow = [
    { icon: faMagnifyingGlass, title: 'Discover', line: 'Read the registry', color: C.blue },
    { icon: faBrain, title: 'Understand', line: 'Inputs, outputs, constraints', color: C.indigo },
    { icon: faCircleCheck, title: 'Choose', line: 'Select a valid capability', color: C.violet },
    { icon: faBolt, title: 'Execute', line: 'Call the advertised URL', color: C.magenta },
    { icon: faRoute, title: 'Continue', line: 'Use processId + next steps', color: C.teal },
  ];
  const startX = 0.88;
  const gap = 2.42;
  slide.addShape(pptx.ShapeType.line, { x: 1.46, y: 2.62, w: 9.66, h: 0, line: { color: 'C9D2E7', width: 3.0, beginArrowType: 'none', endArrowType: 'triangle' } });
  flow.forEach((f, i) => {
    const x = startX + i * gap;
    addIconBadge(slide, f.icon, x, 2.13, 0.98, f.color, C.white, { shadow: true });
    addText(slide, f.title, x - 0.23, 3.26, 1.45, 0.32, { fontSize: 13.2, color: C.ink, bold: true, align: 'center' });
    addText(slide, f.line, x - 0.40, 3.61, 1.78, 0.47, { fontSize: 9.4, color: C.grayText, align: 'center', valign: 'top' });
  });

  addRoundedCard(slide, 0.74, 4.35, 5.93, 2.18, C.navy, { shadow });
  addPill(slide, 'ALREADY AVAILABLE', 1.03, 4.64, 1.58, C.blue, C.white, { h: 0.31, fontSize: 7.4, charSpacing: 0.7 });
  addText(slide, 'Machine-readable operating context', 1.03, 5.02, 4.78, 0.34, { fontSize: 16.5, color: C.white, bold: true });
  addBulletRows(slide, [
    'Capability names, descriptions, schemas, and constraints',
    'Advertised query and execution URLs',
    'Explicit process state and next-step guidance',
    'Structured results, errors, and correlation context',
  ], 1.06, 5.44, 5.14, { rowH: 0.30, fontSize: 9.7, bulletColor: C.cyan, color: 'CFD7E9' });

  addRoundedCard(slide, 6.92, 4.35, 5.66, 2.18, 'F1EDFF', { lineColor: 'DED5FF', lineTransparency: 0, shadow });
  addPill(slide, 'BEFORE AUTONOMOUS ACTION', 7.20, 4.64, 2.18, C.violet, C.white, { h: 0.31, fontSize: 7.4, charSpacing: 0.65 });
  addText(slide, 'Govern the decision boundary', 7.20, 5.02, 4.55, 0.34, { fontSize: 16.5, color: C.ink, bold: true });
  addBulletRows(slide, [
    'Authorization-aware discovery and allowlists',
    'Side-effect, confirmation, and data-sensitivity metadata',
    'Durable idempotency and concurrency guarantees',
    'Registry freshness, redaction, and resource limits',
  ], 7.22, 5.44, 4.98, { rowH: 0.30, fontSize: 9.7, bulletColor: C.violet, color: C.grayText });

  addBrandFooter(slide, false, 'AI opportunity: supervised tool use first; governed autonomy as production guarantees mature.');
}

// Slide 6 — Target consumers
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  addHeader(slide, 'Current direction', 'Initial target consumers', 'Kaleido is the reusable capability layer beneath the systems that orchestrate work, interact with people, or exchange standards-based data.', true);
  addPill(slide, 'CURRENT FOCUS', 10.75, 0.53, 1.50, C.teal, C.navy, { h: 0.31, fontSize: 7.5, charSpacing: 0.8 });

  const useCases = [
    {
      x: 0.73, color: C.violet, icon: faSitemap,
      eyebrow: 'WORKFLOW ENGINES', title: 'Pega and similar platforms',
      body: 'Keep case orchestration, SLAs, assignments, and human work in the workflow platform. Call Kaleido for reusable, typed business capabilities.',
      tags: ['Fewer bespoke connectors', 'Shared business contracts'],
      note: 'Kaleido complements case management; it does not replace it.'
    },
    {
      x: 4.55, color: C.cyan, icon: faPhoneVolume,
      eyebrow: 'IVR / CONTACT CENTER', title: 'Conversational and assisted channels',
      body: 'Use metadata to drive prompts and validation, preserve an explicit process ID, and resume work across transfers, agents, or digital handoffs.',
      tags: ['Consistent prompts', 'Cross-channel continuity'],
      note: 'The same backend action is available to voice, agent desktop, and web.'
    },
    {
      x: 8.37, color: C.teal, icon: faFileContract,
      eyebrow: 'EXTERNAL INTEGRATIONS', title: 'Da Vinci CRD · DTR · PAS',
      body: 'Handle FHIR- and X12-oriented interactions at the edge, then map them into stable internal queries and process steps that can be reused elsewhere.',
      tags: ['Standards at the edge', 'Stable internal model'],
      note: 'CRD discovers requirements, DTR gathers documentation, and PAS supports submission.'
    },
  ];

  useCases.forEach((u) => {
    addRoundedCard(slide, u.x, 1.87, 3.55, 4.63, '10182F', { shadow: false, lineColor: '2E3858', lineTransparency: 0 });
    slide.addShape(pptx.ShapeType.rect, { x: u.x, y: 1.87, w: 3.55, h: 0.09, fill: { color: u.color }, line: { color: u.color } });
    addIconBadge(slide, u.icon, u.x + 0.29, 2.22, 0.70, u.color, C.white, { shadow: false });
    addText(slide, u.eyebrow, u.x + 1.16, 2.21, 2.06, 0.20, { fontSize: 7.8, color: '9EAAC4', bold: true, charSpacing: 1.0 });
    addText(slide, u.title, u.x + 1.16, 2.47, 2.07, 0.58, { fontSize: 16.5, color: C.white, bold: true, valign: 'top' });
    addText(slide, u.body, u.x + 0.29, 3.32, 2.92, 1.29, { fontSize: 11.3, color: 'C3CBDE', valign: 'top', breakLine: true });
    addPill(slide, u.tags[0].toUpperCase(), u.x + 0.29, 4.79, 1.49, '172540', u.color, { h: 0.30, fontSize: 6.9, lineColor: u.color, lineTransparency: 35, charSpacing: 0.4 });
    addPill(slide, u.tags[1].toUpperCase(), u.x + 1.86, 4.79, 1.37, '172540', u.color, { h: 0.30, fontSize: 6.5, lineColor: u.color, lineTransparency: 35, charSpacing: 0.25 });
    slide.addShape(pptx.ShapeType.line, { x: u.x + 0.29, y: 5.36, w: 2.94, h: 0, line: { color: '37425F', width: 0.8 } });
    addText(slide, u.note, u.x + 0.29, 5.52, 2.94, 0.68, { fontSize: 9.0, color: '8F9BB7', italic: true, valign: 'top' });
  });

  addText(slide, 'Complementary, not replacement.', 0.74, 6.66, 3.2, 0.24, { fontSize: 11.3, color: C.white, bold: true });
  addRichText(slide, [
    { text: 'Sources: ', options: { color: '7786A7' } },
    { text: 'Pega workflow automation', options: { color: '9DB3FF', hyperlink: { url: 'https://www.pega.com/products/platform/workflow-automation' } } },
    { text: ' • ', options: { color: '7786A7' } },
    { text: 'HL7 Da Vinci CRD 2.2.1', options: { color: '9DB3FF', hyperlink: { url: 'https://www.hl7.org/fhir/us/davinci-crd/' } } },
    { text: ', ', options: { color: '7786A7' } },
    { text: 'DTR 2.2.0', options: { color: '9DB3FF', hyperlink: { url: 'https://www.hl7.org/fhir/us/davinci-dtr/' } } },
    { text: ', ', options: { color: '7786A7' } },
    { text: 'PAS 2.2.1', options: { color: '9DB3FF', hyperlink: { url: 'https://www.hl7.org/fhir/us/davinci-pas/' } } },
  ], 4.30, 6.65, 7.95, 0.25, { fontSize: 7.2, align: 'right' });
  addBrandFooter(slide, true);
}

// Slide 7 — Prior auth example
{
  const slide = pptx.addSlide('EXEC_LIGHT');
  addLightDecor(slide);
  addHeader(slide, 'Illustrative journey', 'One prior authorization process, many consumers', 'A single process can span multiple services while every channel uses the same metadata, validation, and state model.', false);
  addPill(slide, 'BASED ON THE PRIORAUTH SAMPLE', 9.84, 0.53, 2.50, 'EDE9FE', C.violet, { h: 0.31, fontSize: 7.2, charSpacing: 0.7 });

  // Left channel rail
  addRoundedCard(slide, 0.73, 1.86, 2.02, 4.66, C.navy, { shadow, lineTransparency: 100 });
  addText(slide, 'CONSUMERS', 1.03, 2.14, 1.42, 0.22, { fontSize: 8.3, color: '9DA8C3', bold: true, charSpacing: 1.1 });
  const chan = [
    { icon: faSitemap, label: 'Pega', color: C.violet },
    { icon: faPhoneVolume, label: 'IVR', color: C.cyan },
    { icon: faLaptopCode, label: 'Web / app', color: C.blue },
    { icon: faRobot, label: 'AI agent', color: C.magenta },
    { icon: faGlobe, label: 'Da Vinci', color: C.teal },
  ];
  chan.forEach((c, i) => {
    const yy = 2.59 + i * 0.68;
    addIconBadge(slide, c.icon, 1.03, yy, 0.40, '1B2947', c.color, { shadow: false, lineColor: '2B3A5C', lineTransparency: 0 });
    addText(slide, c.label, 1.58, yy + 0.02, 0.94, 0.31, { fontSize: 11.0, color: C.white, bold: true });
  });
  addText(slide, 'Same capability\ncontract', 1.03, 6.02, 1.30, 0.38, { fontSize: 9.8, color: 'AEB8CF', bold: true, breakLine: true, valign: 'top' });

  // Main journey
  addRoundedCard(slide, 3.02, 1.86, 7.23, 4.66, C.white, { lineColor: 'E0E5F0', lineTransparency: 0, shadow });
  addText(slide, 'PROCESS THREAD', 3.34, 2.14, 2.1, 0.22, { fontSize: 8.3, color: C.indigo, bold: true, charSpacing: 1.1 });
  addPill(slide, 'processId', 8.98, 2.05, 0.86, 'EEF2FF', C.indigo, { h: 0.31, fontSize: 7.1, lineColor: 'C8D1EE', lineTransparency: 0, charSpacing: 0.4 });
  slide.addShape(pptx.ShapeType.line, { x: 3.78, y: 4.03, w: 5.92, h: 0, line: { color: 'AAB4CD', width: 2.1, endArrowType: 'triangle' } });

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
    addText(slide, j.label, j.x - 0.18, 4.58, 1.16, 0.56, { fontSize: 10.2, color: C.ink, bold: true, align: 'center', valign: 'top', breakLine: true });
    addPill(slide, j.service, j.x - 0.10, 5.28, 1.02, 'F2F4F8', C.grayText, { h: 0.29, fontSize: 7.0, bold: true, lineColor: 'DCE1EB', lineTransparency: 0 });
    if (i < journey.length - 1) {
      addIcon(slide, faArrowRightLong, j.x + 0.86, 3.86, 0.22, '8793AE');
    }
  });
  addText(slide, 'Queryable supplies context and reference data; Process manages the resumable business journey.', 3.34, 5.88, 6.52, 0.39, { fontSize: 10.2, color: C.grayText, italic: true, align: 'center' });

  // Right outcome rail
  addRoundedCard(slide, 10.52, 1.86, 2.06, 4.66, 'F1EDFF', { lineColor: 'DED6FF', lineTransparency: 0, shadow });
  addText(slide, 'SHARED OUTCOME', 10.81, 2.14, 1.48, 0.22, { fontSize: 8.2, color: C.violet, bold: true, charSpacing: 0.9 });
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
    addText(slide, o[0], 11.20, yy, 1.05, 0.31, { fontSize: 10.1, color: C.ink, bold: true });
  });
  addText(slide, 'The channel can change without redefining the business capability.', 10.81, 5.94, 1.49, 0.42, { fontSize: 9.0, color: C.grayText, italic: true, valign: 'top' });

  addBrandFooter(slide, false, 'Illustrative: service and workflow concepts are grounded in the repository’s PriorAuth sample.');
}

// Slide 8 — Executive takeaway
{
  const slide = pptx.addSlide('EXEC_DARK');
  addDarkDecor(slide);
  slide.addImage({ path: MARK_LIGHT, x: 10.10, y: 0.78, w: 2.60, h: 3.0, transparency: 82 });
  addText(slide, 'EXECUTIVE TAKEAWAY', 0.74, 0.61, 4.3, 0.22, { fontSize: 8.5, color: '9BA7C2', bold: true, charSpacing: 1.6 });
  addText(slide, 'Standardize once.\nReuse everywhere.', 0.74, 1.00, 7.76, 1.28, { fontSize: 34, color: C.white, bold: true, valign: 'top', breakLine: true });
  addText(slide, 'Kaleido creates a common language between business services and the systems that need to use them.', 0.74, 2.45, 7.74, 0.52, { fontSize: 14.2, color: 'BEC7DB', valign: 'top' });

  const outcomes = [
    { x: 0.74, y: 3.25, icon: faLayerGroup, color: C.blue, title: 'Shared capability contracts', body: 'A consistent way to expose queries and actions.' },
    { x: 3.87, y: 3.25, icon: faCodeBranch, color: C.violet, title: 'Less one-off integration', body: 'Channels reuse metadata, URLs, validation, and state.' },
    { x: 7.00, y: 3.25, icon: faChartLine, color: C.teal, title: 'Resumable, observable work', body: 'Explicit process identity and correlation across services.' },
    { x: 10.13, y: 3.25, icon: faShieldHalved, color: C.magenta, title: 'Governed path to AI', body: 'Supervised tool use now; safer autonomy as controls mature.' },
  ];
  outcomes.forEach((o) => {
    addRoundedCard(slide, o.x, o.y, 2.47, 1.55, '111A35', { shadow: false, lineColor: '303B5B', lineTransparency: 0 });
    addIconBadge(slide, o.icon, o.x + 0.20, o.y + 0.23, 0.51, o.color, C.white, { shadow: false });
    addText(slide, o.title, o.x + 0.84, o.y + 0.18, 1.42, 0.48, { fontSize: 12.2, color: C.white, bold: true, valign: 'top' });
    addText(slide, o.body, o.x + 0.20, o.y + 0.83, 2.05, 0.49, { fontSize: 9.3, color: 'AEB8CD', valign: 'top' });
  });

  addText(slide, 'NEAR-TERM DIRECTION', 0.74, 5.28, 3.0, 0.22, { fontSize: 8.2, color: '9BA7C2', bold: true, charSpacing: 1.1 });
  const roadmap = [
    { x: 0.74, n: '01', t: 'PREVIEW', d: 'Publish and validate\nconsumer packages', color: C.blue },
    { x: 3.73, n: '02', t: 'HARDEN', d: 'State, idempotency,\nprivacy, authorization', color: C.violet },
    { x: 6.72, n: '03', t: 'PROVE', d: 'Pega, IVR, and\nDa Vinci adapters', color: C.teal },
    { x: 9.71, n: '04', t: 'SCALE', d: 'Architecture and\ntech-lead guidance', color: C.magenta },
  ];
  slide.addShape(pptx.ShapeType.line, { x: 1.19, y: 5.98, w: 9.96, h: 0, line: { color: '47516F', width: 1.4 } });
  roadmap.forEach((r) => {
    addIconBadge(slide, faCircleCheck, r.x, 5.70, 0.52, r.color, C.white, { shadow: false });
    addText(slide, r.n, r.x + 0.65, 5.66, 0.36, 0.22, { fontSize: 7.6, color: r.color, bold: true, charSpacing: 0.6 });
    addText(slide, r.t, r.x + 1.02, 5.66, 1.24, 0.22, { fontSize: 9.5, color: C.white, bold: true, charSpacing: 0.4 });
    addText(slide, r.d, r.x + 0.65, 6.04, 1.94, 0.58, { fontSize: 9.4, color: '9FAAC2', valign: 'top', breakLine: true });
  });
  addBrandFooter(slide, true, 'Kaleido — typed, discoverable, stateful business capabilities.');
}

pptx.writeFile({ fileName: '/mnt/data/Kaleido_Executive_Overview.pptx' });
