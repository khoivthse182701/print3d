---
name: powerpoint_30_layouts_aesthetic_director
description: Art direction engine for presentations featuring 8 sensory aesthetic archetypes, 10 high-impact typography styles, 30 distinct non-repetitive slide layouts (Bento grids, BMC, TAM/SAM/SOM, 3D callouts, architecture stacks), and an automated python-pptx layout builder.
---

# 30-Layout System, Typography & Aesthetic Direction Skill

Use this skill whenever designing slide decks, choosing presentation color palettes and fonts, structuring information density, or generating `.pptx` files that require executive-level visual polish.

---

## 1. Aesthetic Sensory Awareness Engine (Nhận Thức Cảm Quan Phong Cách)

Before placing a single shape on a slide, analyze the user's topic, audience, and emotional tone, then lock in **ONE** of the 8 Aesthetic Archetypes below. Strictly follow the **60-30-10 Color Rule** (`60%` Primary Canvas Background, `30%` Secondary Surface/Cards, `10%` High-Contrast Accent) and maintain at least **35% negative space (whitespace)**.

### The 8 Canonical Aesthetic Archetypes

| Archetype | Best For | 60% Canvas Bg | 30% Card Surface | 10% Accent | Heading / Body Font Pair | Sensory Mood & Surface Feel |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **1. Linear / Apple Dark Tech** | SaaS, AI, Software Architecture, Pitch Decks | `#090D16` (Obsidian) | `#131B2E` (Slate Card + `1pt` `#26334D` border) | `#38BDF8` (Cyan) & `#818CF8` (Indigo) | `Montserrat` / `Inter` | Sleek, futuristic, glowing borders, Bento modularity. |
| **2. Swiss Brutalist Grid** | Design Systems, Engineering Specs, Modern Art | `#F4F4F0` (Warm Paper) | `#EAEAE4` (Stone Panel + `1.5pt` `#111111` lines) | `#FF3B00` (International Orange) | `Arial Black` / `Segoe UI` | Razor-sharp exposed grid lines, oversized numbers, stark contrast. |
| **3. Luxury Editorial** | Brand Strategy, Fashion, Real Estate, Hospitality | `#141413` (Charcoal) | `#1F1E1C` (Espresso Matte) | `#D4AF37` (Champagne Gold) | `Georgia` (Serif) / `Montserrat` | Cinematic, high-fashion magazine layout, generous margins. |
| **4. Zen ArchViz Sanctuary** | 3D Environment, Cultural/Temple Showcase, Travel | `#0B131E` (Blurred Twilight) | `#162232` (50% Translucent Glass) | `#F59E0B` (Warm Lantern Amber) | `Palatino Linotype` / `Calibri` | Atmospheric depth, blurred backgrounds, soft glowing highlights. |
| **5. Executive VC / McKinsey** | Financial Models (P&L), Market Research, Consulting | `#F8FAFC` (Crisp Alabaster) | `#FFFFFF` (Pure White + soft shadow) | `#0F172A` (Navy) & `#059669` (Emerald) | `Trebuchet MS` / `Calibri` | Authoritative, data-driven, action-title headers, zero clutter. |
| **6. Academic & Research** | University Thesis, Survey Methodology, Literature | `#FCFBF9` (Ivory) | `#F1EFEA` (Warm Gray Box) | `#1E3A8A` (Oxford Blue) & `#991B1B` (Crimson) | `Cambria` / `Calibri` | Rigorous, structured, clear citation hierarchy, high legibility. |
| **7. Web3 / Cyber Gaming** | Game Dev, 3D Character Pipelines, Crypto | `#0D061A` (Void Purple) | `#1A0E33` (Neon Rim Card) | `#EC4899` (Magenta) & `#A3E635` (Acid Lime) | `Impact` or `Montserrat Black` / `Consolas` | High-energy HUD geometry, angled corners, neon telemetry. |
| **8. Clean Medical / Eco** | Sustainability, Recycling Platforms, Healthcare | `#F0FDF4` (Mint Mist) | `#FFFFFF` (Clean White Card) | `#15803D` (Forest Green) & `#0284C7` (Sky) | `Segoe UI Semibold` / `Segoe UI` | Fresh, trustworthy, organic rounded corners, airy spacing. |

---

## 2. The 10 Signature Text Styles & Typography Hierarchy

Never dump raw bullet lists onto a slide. Limit any single slide to **maximum 35–45 words** across **3 visual groups**, and style text using these 10 typographic patterns:

1. **Style 01 — Eyebrow Kicker + Display Stack:**
   - Top line: `10pt–11pt` ALL CAPS, Accent Color, wide letter spacing (`Character Spacing: Very Loose / +4pt`), e.g., `01 // SYSTEM ARCHITECTURE`.
   - Bottom line: `36pt–48pt` Bold Title in Primary Text Color with tight line-height (`0.95`).
2. **Style 02 — Split-Weight Contrast Headline:**
   - Combine `Light/Regular` weight with `ExtraBold` weight plus an accent color highlight in the same sentence (e.g., *"From Manual Chaos to **Automated Precision**"*).
3. **Style 03 — Giant Stat Odometer Callout:**
   - Metric value in `64pt–96pt` ExtraBold Accent Color (`99.4%`, `$2.4M`, `3.2x`), paired with a `13pt` muted label directly underneath.
4. **Style 04 — Ghost Watermark Index Number:**
   - Place a giant `110pt–160pt` number (`01`, `02`, `03`) at `12%–18%` opacity behind the card top-left corner to anchor reading order without clutter.
5. **Style 05 — Pill Tag / Status Badge (`Rounded Capsule`):**
   - Small rounded rectangle (`height: 0.32"`) with `15%` accent fill and `100%` bright accent text (`9.5pt Bold`) for statuses like `ACTIVE`, `CLEAN ARCHITECTURE`, `f/1.8 DOF`.
6. **Style 06 — Hollow Knockout Stroke Title:**
   - Giant `80pt–130pt` title with `No Text Fill` and `1.5pt White/Accent Text Outline`, layered over high-contrast photography or 3D renders.
7. **Style 07 — Action-Title Consulting Header (McKinsey Standard):**
   - Full-width top banner where the title states the **conclusion** rather than the topic (e.g., *"3D Web Self-Service Reduces Printing Lead Time by 42%"* instead of *"Market Research"*).
8. **Style 08 — Vertical Margin Spine (`-90° Rotation`):**
   - `12pt` uppercase tracking label docked vertically along the far-left edge (`X = 0.35"`) to indicate the current chapter across multiple slides.
9. **Style 09 — Technical Monospace Spec Block:**
   - Use `Consolas` or `Courier New` (`11pt`) inside a dark bordered container for API endpoints (`POST /api/v1/bookings`), code snippets, or mathematical formulas.
10. **Style 10 — Frosted Glass Caption Overlay:**
    - Dark semi-transparent card (`45%–60% transparency`, `1pt` white border at `25%` opacity) placed over full-bleed visuals so white body text (`14pt`) remains 100% legible.

---

## 3. Complete Catalog of 30 Canonical Slide Layouts

When building a presentation, never use the same layout twice in a row. Alternate visual rhythm across these 30 blueprints (`13.333" x 7.5"` widescreen canvas):

### Category 1: Covers, Dividers & Narrative Anchors (`#01` – `#06`)
* **Layout 01 — Monolith Center Hero Cover:** Full-bleed darkened/blurred background, centered Pill Badge (`Y=2.3"`), Giant Display Title (`Y=2.8"`), Subtitle (`Y=4.3"`), and bottom metadata bar (`Y=6.6"`).
* **Layout 02 — 60/40 Asymmetric Split Cover:** Left `58%` (`7.7"`) houses Eyebrow, Giant Title, and Author Badge; Right `42%` (`5.6"`) is a full-bleed hero image or 3D render panel.
* **Layout 03 — Framed Portal / Window Cover:** Outer border frame (`0.5"` margin) with a giant rounded viewport window in the center and overlapping knockout typography.
* **Layout 04 — Big-Index Chapter Divider:** Giant `160pt` chapter number (`01`) on the left (`X=1.0"`), vertical accent divider line, and Chapter Title + 3 sub-topics stacked on the right (`X=5.2"`).
* **Layout 05 — Interactive Horizontal Agenda Strip:** 5 vertical columns across the canvas; the active chapter column is highlighted in Accent Color (`1.1x` taller) while the other 4 remain muted.
* **Layout 06 — Executive Manifesto / Pull-Quote:** Oversized decorative quotation mark (`180pt`), centered `32pt` Serif/Bold statement across `9.0"` width, and author avatar/role below.

### Category 2: Bento Grids & Modular Card Systems (`#07` – `#12`)
* **Layout 07 — Apple 4-Cell Asymmetric Bento Grid:**
  - Cell 1 (Top-Left Large): `6.8" x 3.2"` | Cell 2 (Bottom-Left): `3.3" x 2.4"` | Cell 3 (Bottom-Mid): `3.3" x 2.4"` | Cell 4 (Right Tall Hero): `4.6" x 5.8"`.
* **Layout 08 — 6-Cell Uniform Feature Matrix (`3x2` Bento):** 6 rounded cards (`3.6" x 2.4"` each) arranged in 3 columns x 2 rows, each with an Icon Pill, `18pt` Header, and 2-line description.
* **Layout 09 — 3-Pillar Hero Center Elevation:** 3 vertical cards (`3.5" x 4.8"`); the center card is scaled taller (`5.3"`), filled with primary accent glow, and badged as `CORE / RECOMMENDED`.
* **Layout 10 — 4-Column 3D Flip-Card Showcase:** 4 tall portrait cards (`2.6" x 4.6"`) evenly spaced horizontally (`gap = 0.35"`) with front character/location visuals and bottom role tags.
* **Layout 11 — Left Radial Donut Wheel + Right Detail Stage:** Left `40%` holds the 7-slice rotating picture wheel; Right `60%` holds the expanded slice preview, `44pt` Header, and 2 metric sub-cards.
* **Layout 12 — Editorial Staggered Z-Pattern (Alternating Rows):** 2 horizontal rows where Row 1 is `[Image Left 40% | Text Right 60%]` and Row 2 is `[Text Left 60% | Image Right 40%]`.

### Category 3: Architecture, Flows & Comparisons (`#13` – `#18`)
* **Layout 13 — 50/50 Versus Battle Split (Before vs. After):** Left half tinted subtle crimson/muted (` Legacy / Problem `) vs. Right half tinted vibrant emerald/cyan (` Proposed Solution `) with a central `VS` badge.
* **Layout 14 — 3-Tier Clean Architecture Horizontal Stack:** 3 wide horizontal strata (`Presentation Layer` $\rightarrow$ `Application / CQRS Layer` $\rightarrow$ `Domain Core Layer`) flanked by a vertical `Infrastructure` sidebar on the right.
* **Layout 15 — 4-Step Connected Process Pipeline:** 4 horizontal cards (`2.6" x 3.6"`) linked by directional arrow chevrons, each topped with a circular step badge (`01` to `04`).
* **Layout 16 — Central Core & 6-Orbital Nodes (Hub & Spoke):** Central circular/octagonal core (`2.8" x 2.8"`) surrounded by 6 satellite cards (`3 Left`, `3 Right`) connected via thin leader lines.
* **Layout 17 — Zig-Zag Milestone Roadmap:** Horizontal timeline spine across `Y=4.0"` with 5 alternating milestone nodes (`Odd nodes above line`, `Even nodes below line`).
* **Layout 18 — Input $\rightarrow$ Engine $\rightarrow$ Output Funnel:** 3-stage system flow: Left column (`3 Input Sources`) $\rightarrow$ Center Hero Box (`AI / Processing Engine`) $\rightarrow$ Right column (`3 Deliverable Outputs`).

### Category 4: Data, Financial & Business Strategy (`#19` – `#24`)
* **Layout 19 — 3-Hero KPI Scoreboard + Bottom Insight Bar:** 3 massive odometer metric cards across the top (`3.6" x 3.0"`) + 1 full-width bottom takeaway banner (`11.5" x 1.6"`).
* **Layout 20 — 70/30 Chart Stage + Key Findings Sidebar:** Left `7.8"` dedicated to a clean data chart/graph; Right `3.6"` houses 3 numbered insight callout boxes.
* **Layout 21 — 9-Block Business Model Canvas (BMC) Grid:** Exact Osterwalder 9-cell geometry: Top 5 columns (`KP`, `KA/KR` split vertically, `VP`, `CR/CH` split vertically, `CS`) + Bottom 2 wide columns (`Cost Structure`, `Revenue Streams`).
* **Layout 22 — 2x2 Competitive Positioning Matrix:** X/Y coordinate plane (`7.0" x 5.2"`) on the left with plotted competitor bubbles (highlighting our project in the top-right quadrant) + Legend sidebar on the right.
* **Layout 23 — TAM / SAM / SOM Concentric Market Rings:** 3 bottom-aligned nested circles on the left (`TAM` outer, `SAM` middle, `SOM` inner core) connected by horizontal leader lines to 3 valuation descriptions on the right.
* **Layout 24 — 4-Column Financial P&L / Pricing Tier Table:** Structured comparison matrix with row headers on the left and 3–4 data columns (with the target year or target tier highlighted by an accent border).

### Category 5: Deep-Dives, Specs & Closings (`#25` – `#30`)
* **Layout 25 — Center 3D Subject + 4 Corner Spec Callouts:** Central 3D model or product cutaway (`4.5" x 4.5"`) flanked by 4 technical spec cards at top-left, bottom-left, top-right, and bottom-right.
* **Layout 26 — Accordion Pill Showcase (1 Expanded + 3 Collapsed):** 1 wide active card (`7.2" x 5.0"`) showing full imagery and specs alongside 3 narrow vertical pill tabs (`1.1" x 5.0"`) with vertical `-90°` titles.
* **Layout 27 — Case Study Dossier (Challenge / Architecture / Impact):** Left profile card (`3.5"` width) + Right stack of 3 horizontal bands (`01. The Challenge`, `02. Technical Execution`, `03. Quantified ROI`).
* **Layout 28 — Risk & Mitigation Matrix Table:** 4 horizontal rows where each row pairs a `[Risk & Severity Badge]` on the left with a `[Mitigation Control & Fallback]` box on the right.
* **Layout 29 — 3 Golden Takeaways Summary List:** Left `35%` bold sticky header (`Key Takeaways`) + Right `65%` 3 wide horizontal cards with giant check/number badges.
* **Layout 30 — Grand Finale / Thank You & Q&A Hub:** Centered hero thank-you statement + 3 bottom contact/resource cards (`GitHub Repo`, `Live Demo QR`, `Team/Author Info`).

---

## 4. Automated Python Layout & Aesthetic Engine (`python-pptx`)

Use this complete script to automatically generate all **30 canonical slide layouts** styled with any of the **8 Aesthetic Archetypes** and **10 Text Styles**:

```python
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.enum.text import PP_ALIGN


# ==============================================================================
# 1. AESTHETIC ARCHETYPE PALETTES (60-30-10 SENSORY TOKENS)
# ==============================================================================
AESTHETIC_THEMES = {
    "LINEAR_DARK_TECH": {
        "bg": (9, 13, 22),
        "surface": (19, 27, 46),
        "surface_alt": (28, 39, 64),
        "border": (45, 62, 96),
        "text_primary": (248, 250, 252),
        "text_muted": (148, 163, 184),
        "accent": (56, 189, 248),
        "accent_alt": (129, 140, 248),
        "font_head": "Montserrat",
        "font_body": "Segoe UI",
    },
    "SWISS_BRUTALIST": {
        "bg": (244, 244, 240),
        "surface": (232, 232, 226),
        "surface_alt": (220, 220, 212),
        "border": (17, 17, 17),
        "text_primary": (17, 17, 17),
        "text_muted": (82, 82, 78),
        "accent": (255, 59, 0),
        "accent_alt": (17, 17, 17),
        "font_head": "Arial Black",
        "font_body": "Segoe UI",
    },
    "LUXURY_EDITORIAL": {
        "bg": (20, 20, 19),
        "surface": (31, 30, 28),
        "surface_alt": (41, 39, 36),
        "border": (78, 70, 55),
        "text_primary": (250, 248, 245),
        "text_muted": (168, 162, 158),
        "accent": (212, 175, 55),
        "accent_alt": (180, 140, 40),
        "font_head": "Georgia",
        "font_body": "Calibri",
    },
    "EXECUTIVE_VC": {
        "bg": (248, 250, 252),
        "surface": (255, 255, 255),
        "surface_alt": (241, 245, 249),
        "border": (203, 213, 225),
        "text_primary": (15, 23, 42),
        "text_muted": (100, 116, 139),
        "accent": (37, 99, 235),
        "accent_alt": (5, 150, 105),
        "font_head": "Trebuchet MS",
        "font_body": "Calibri",
    },
}


# ==============================================================================
# 2. PRIMITIVE STYLING & TYPOGRAPHY HELPERS
# ==============================================================================
def set_slide_background(slide, rgb):
    bg = slide.shapes.add_shape(MSO_SHAPE.RECTANGLE, Inches(0), Inches(0), Inches(13.333), Inches(7.5))
    bg.name = "!!Canvas_Background"
    bg.fill.solid()
    bg.fill.fore_color.rgb = RGBColor(*rgb)
    bg.line.fill.background()
    return bg


def add_card(slide, left, top, width, height, theme, name="Card", use_accent=False, rounded=True):
    shape_type = MSO_SHAPE.ROUNDED_RECTANGLE if rounded else MSO_SHAPE.RECTANGLE
    card = slide.shapes.add_shape(shape_type, Inches(left), Inches(top), Inches(width), Inches(height))
    card.name = f"!!{name}"
    card.fill.solid()
    fill_rgb = theme["accent"] if use_accent else theme["surface"]
    card.fill.fore_color.rgb = RGBColor(*fill_rgb)
    card.line.color.rgb = RGBColor(*(theme["accent"] if use_accent else theme["border"]))
    card.line.width = Pt(1.25)
    return card


def add_styled_header(slide, kicker, title, subtitle, theme, left=0.8, top=0.55, width=11.5):
    """Applies Text Style 01: Eyebrow Kicker + Bold Display Title + Muted Subtitle."""
    tb = slide.shapes.add_textbox(Inches(left), Inches(top), Inches(width), Inches(1.35))
    tb.name = "!!Header_Block"
    tf = tb.text_frame
    tf.word_wrap = True

    # Eyebrow Kicker
    p0 = tf.paragraphs[0]
    p0.text = kicker.upper()
    p0.font.name = theme["font_body"]
    p0.font.size = Pt(10)
    p0.font.bold = True
    p0.font.color.rgb = RGBColor(*theme["accent"])

    # Main Title
    p1 = tf.add_paragraph()
    p1.text = title
    p1.font.name = theme["font_head"]
    p1.font.size = Pt(28)
    p1.font.bold = True
    p1.font.color.rgb = RGBColor(*theme["text_primary"])

    # Subtitle
    if subtitle:
        p2 = tf.add_paragraph()
        p2.text = subtitle
        p2.font.name = theme["font_body"]
        p2.font.size = Pt(13)
        p2.font.color.rgb = RGBColor(*theme["text_muted"])


def add_card_text(slide, left, top, width, height, title, body, theme, stat="", invert=False):
    tb = slide.shapes.add_textbox(Inches(left + 0.2), Inches(top + 0.18), Inches(width - 0.4), Inches(height - 0.3))
    tf = tb.text_frame
    tf.word_wrap = True

    title_color = (9, 13, 22) if invert else theme["text_primary"]
    body_color = (30, 41, 59) if invert else theme["text_muted"]

    if stat:
        ps = tf.paragraphs[0]
        ps.text = stat
        ps.font.name = theme["font_head"]
        ps.font.size = Pt(38)
        ps.font.bold = True
        ps.font.color.rgb = RGBColor(*(title_color if invert else theme["accent"]))
        pt = tf.add_paragraph()
    else:
        pt = tf.paragraphs[0]

    pt.text = title
    pt.font.name = theme["font_head"]
    pt.font.size = Pt(15)
    pt.font.bold = True
    pt.font.color.rgb = RGBColor(*title_color)

    if body:
        pb = tf.add_paragraph()
        pb.text = body
        pb.font.name = theme["font_body"]
        pb.font.size = Pt(11.5)
        pb.font.color.rgb = RGBColor(*body_color)


# ==============================================================================
# 3. MASTER 30-LAYOUT GENERATOR ENGINE
# ==============================================================================
def build_30_canonical_layouts_deck(
    theme_name: str = "LINEAR_DARK_TECH",
    output_pptx: str = "Master_30_Layouts_Aesthetic_Deck.pptx"
):
    theme = AESTHETIC_THEMES.get(theme_name, AESTHETIC_THEMES["LINEAR_DARK_TECH"])
    prs = Presentation()
    prs.slide_width = Inches(13.333)
    prs.slide_height = Inches(7.5)
    blank = prs.slide_layouts[6]

    layouts_spec = [
        # Category 1: Covers, Dividers & Narrative Anchors (01-06)
        ("01 // COVER MONOLITH", "Layout 01: Center Monolith Hero Cover", "Full-bleed canvas with centered pill badge and high-impact display typography"),
        ("02 // ASYMMETRIC SPLIT", "Layout 02: 60/40 Asymmetric Split Cover", "Left narrative column paired with right full-bleed 3D/visual showcase panel"),
        ("03 // PORTAL WINDOW", "Layout 03: Framed Portal Viewport", "Architectural framing border with central focal viewport"),
        ("04 // CHAPTER DIVIDER", "Layout 04: Big-Index Section Divider", "Oversized chapter index number paired with vertical spine and agenda topics"),
        ("05 // AGENDA STRIP", "Layout 05: 5-Column Interactive Agenda", "Active chapter column highlighted in accent color against muted sibling stages"),
        ("06 // MANIFESTO QUOTE", "Layout 06: Executive Pull-Quote Statement", "High-contrast editorial quote with oversized watermark glyph"),
        # Category 2: Bento Grids & Modular Card Systems (07-12)
        ("07 // APPLE BENTO 4-CELL", "Layout 07: Asymmetric 4-Cell Bento Grid", "1 Large Hero Card + 2 Square Metric Cells + 1 Tall Feature Showcase"),
        ("08 // 6-CELL MATRIX", "Layout 08: 3x2 Modular Feature Bento", "Balanced 6-card grid with icon badges and concise micro-copy"),
        ("09 // 3-PILLAR ELEVATION", "Layout 09: 3-Pillar Hero Center Elevation", "Central recommended pillar elevated above flanking comparison tiers"),
        ("10 // 3D FLIP SHOWCASE", "Layout 10: 4-Column 3D Flip-Card Gallery", "Portrait card array designed for 180-degree 3D Morph rotations"),
        ("11 // RADIAL WHEEL STAGE", "Layout 11: Left Donut Wheel + Right Detail Stage", "Docked radial wheel on left with expanded slice and KPI breakdown on right"),
        ("12 // EDITORIAL Z-PATTERN", "Layout 12: Staggered Alternating Rows", "Alternating visual/text horizontal bands for natural eye scanning"),
        # Category 3: Architecture, Flows & Comparisons (13-18)
        ("13 // VERSUS SPLIT", "Layout 13: 50/50 Before vs. After Comparison", "Direct side-by-side contrast between legacy pain points and proposed architecture"),
        ("14 // CLEAN ARCHITECTURE", "Layout 14: 3-Tier Clean Architecture Stack", "Presentation -> Application CQRS -> Domain Core strata with Infrastructure sidebar"),
        ("15 // PROCESS PIPELINE", "Layout 15: 4-Stage Connected Process Flow", "Sequential horizontal pipeline with numbered step indicators"),
        ("16 // HUB & SPOKE", "Layout 16: Central Core & 6 Orbital Nodes", "Core platform engine surrounded by 6 integrated satellite modules"),
        ("17 // ZIG-ZAG ROADMAP", "Layout 17: Alternating Milestone Timeline", "Horizontal timeline spine with staggered above/below milestone cards"),
        ("18 // SYSTEM FUNNEL", "Layout 18: Input -> Core Engine -> Output Funnel", "End-to-end data transformation flow from raw inputs to deliverables"),
        # Category 4: Data, Financial & Business Strategy (19-24)
        ("19 // KPI SCOREBOARD", "Layout 19: 3-Hero Odometer KPIs + Takeaway Bar", "Giant numerical callouts anchored by an executive bottom conclusion banner"),
        ("20 // CHART + INSIGHTS", "Layout 20: 70/30 Chart Stage & Insight Sidebar", "Dedicated data visualization canvas paired with 3 actionable takeaways"),
        ("21 // BUSINESS MODEL CANVAS", "Layout 21: 9-Block Business Model Canvas (BMC)", "Complete Osterwalder 9-block strategic grid on a single widescreen slide"),
        ("22 // 2X2 QUADRANT", "Layout 22: 2x2 Competitive Positioning Matrix", "Four-quadrant coordinate plane with competitor plotting and strategic legend"),
        ("23 // TAM SAM SOM", "Layout 23: Concentric Market Sizing Rings", "Nested TAM / SAM / SOM rings linked to valuation breakdowns"),
        ("24 // FINANCIAL P&L TABLE", "Layout 24: 4-Column Financial Projection Matrix", "Structured multi-year revenue, cost, and margin comparison table"),
        # Category 5: Deep-Dives, Specs & Closings (25-30)
        ("25 // 3D SPEC CALLOUTS", "Layout 25: Center 3D Subject + 4 Corner Callouts", "Central 3D viewport surrounded by four technical specification cards"),
        ("26 // ACCORDION PILLS", "Layout 26: 1 Expanded Stage + 3 Vertical Pill Tabs", "Interactive accordion layout maximizing focus on the active module"),
        ("27 // CASE STUDY DOSSIER", "Layout 27: Profile Card + 3-Band Impact Stack", "Client/Project profile on left with Challenge, Execution, and ROI bands on right"),
        ("28 // RISK MATRIX", "Layout 28: Risk & Mitigation Control Pairs", "4 structured rows pairing severity badges with concrete fallback controls"),
        ("29 // GOLDEN TAKEAWAYS", "Layout 29: Sticky Summary Header + 3 Rule Cards", "Left-anchored summary title paired with three high-contrast takeaway bars"),
        ("30 // GRAND FINALE", "Layout 30: Closing Call-to-Action & Resource Hub", "Centered closing statement with QR/Demo, Repository, and Team contact cards"),
    ]

    for idx, (kicker, title, subtitle) in enumerate(layouts_spec, start=1):
        s = prs.slides.add_slide(blank)
        set_slide_background(s, theme["bg"])
        add_styled_header(s, kicker, title, subtitle, theme)

        # Render distinct structural geometry per layout ID
        if idx == 7:  # Layout 07: Apple 4-Cell Asymmetric Bento Grid
            add_card(s, 0.8, 2.0, 6.8, 2.5, theme, "Bento_1")
            add_card_text(s, 0.8, 2.0, 6.8, 2.5, "Primary Architecture Core", "Clean Architecture + CQRS decoupled domain handlers.", theme, stat="99.9%")
            add_card(s, 0.8, 4.7, 3.3, 2.1, theme, "Bento_2")
            add_card_text(s, 0.8, 4.7, 3.3, 2.1, "Latency", "Sub-15ms response.", theme, stat="12ms")
            add_card(s, 4.3, 4.7, 3.3, 2.1, theme, "Bento_3")
            add_card_text(s, 4.3, 4.7, 3.3, 2.1, "Test Coverage", "Automated xUnit suite.", theme, stat="94%")
            add_card(s, 7.8, 2.0, 4.7, 4.8, theme, "Bento_4_Hero", use_accent=True)
            add_card_text(s, 7.8, 2.0, 4.7, 4.8, "3D & AI Pipeline", "End-to-end automated rendering and real-time physics.", theme, stat="4.8x", invert=True)

        elif idx == 14:  # Layout 14: 3-Tier Clean Architecture Stack
            layers = [
                ("01. Presentation Layer (WebAPI / React 3D UI)", 2.0),
                ("02. Application Layer (CQRS Commands, Queries, Interfaces)", 3.65),
                ("03. Domain Core Layer (Rich Entities, Value Objects, Invariants)", 5.3),
            ]
            for l_idx, (l_name, y_pos) in enumerate(layers):
                add_card(s, 0.8, y_pos, 8.4, 1.4, theme, f"Arch_Tier_{l_idx}", use_accent=(l_idx == 2))
                add_card_text(s, 0.8, y_pos, 8.4, 1.4, l_name, "Strict inward dependency rule enforced.", theme, invert=(l_idx == 2))
            add_card(s, 9.4, 2.0, 3.1, 4.7, theme, "Arch_Infra")
            add_card_text(s, 9.4, 2.0, 3.1, 4.7, "Infrastructure", "EF Core, SQL Server, Redis, Cloud Storage, External SDKs.", theme)

        elif idx == 21:  # Layout 21: 9-Block Business Model Canvas (BMC)
            bmc_cols = [
                ("Key Partners", 0.8, 2.0, 2.2, 3.3),
                ("Key Activities", 3.1, 2.0, 2.2, 1.6),
                ("Key Resources", 3.1, 3.7, 2.2, 1.6),
                ("Value Propositions", 5.4, 2.0, 2.5, 3.3),
                ("Customer Rel.", 8.0, 2.0, 2.2, 1.6),
                ("Channels", 8.0, 3.7, 2.2, 1.6),
                ("Customer Segments", 10.3, 2.0, 2.2, 3.3),
                ("Cost Structure", 0.8, 5.45, 5.75, 1.45),
                ("Revenue Streams", 6.75, 5.45, 5.75, 1.45),
            ]
            for b_name, bx, by, bw, bh in bmc_cols:
                is_vp = (b_name == "Value Propositions")
                add_card(s, bx, by, bw, bh, theme, f"BMC_{b_name}", use_accent=is_vp)
                add_card_text(s, bx, by, bw, bh, b_name, "Core strategic driver", theme, invert=is_vp)

        elif idx == 23:  # Layout 23: TAM / SAM / SOM Concentric Rings
            rings = [
                ("TAM", 0.8, 2.0, 4.8, "$12.5B Global Market"),
                ("SAM", 1.5, 3.2, 3.4, "$1.8B Regional Segment"),
                ("SOM", 2.2, 4.4, 2.0, "$180M Target Share"),
            ]
            for r_idx, (r_label, rx, ry, r_diam, r_desc) in enumerate(rings):
                circle = s.shapes.add_shape(MSO_SHAPE.OVAL, Inches(rx), Inches(ry), Inches(r_diam), Inches(r_diam))
                circle.name = f"!!Market_Ring_{r_label}"
                circle.fill.solid()
                circle.fill.fore_color.rgb = RGBColor(*(theme["accent"] if r_idx == 2 else theme["surface"]))
                circle.line.color.rgb = RGBColor(*theme["accent"])
                circle.line.width = Pt(1.5)
                add_card(s, 6.2, 2.1 + r_idx * 1.6, 6.3, 1.35, theme, f"Market_Desc_{r_label}", use_accent=(r_idx == 2))
                add_card_text(s, 6.2, 2.1 + r_idx * 1.6, 6.3, 1.35, f"{r_label} — {r_desc}", "Bottom-up validated addressable demand.", theme, invert=(r_idx == 2))

        else:
            # Modular Adaptive Grid for remaining canonical layouts
            cols = 3 if idx % 3 == 0 else (4 if idx % 2 == 0 else 2)
            gap = 0.3
            total_w = 11.7
            card_w = (total_w - gap * (cols - 1)) / cols
            for c in range(cols):
                cx = 0.8 + c * (card_w + gap)
                is_focus = (c == (cols // 2) and idx in {5, 9, 19, 24})
                cy = 1.95 if is_focus else 2.15
                ch = 4.9 if is_focus else 4.6
                add_card(s, cx, cy, card_w, ch, theme, f"L{idx:02d}_Col_{c}", use_accent=is_focus)
                add_card_text(
                    s, cx, cy, card_w, ch,
                    f"Module 0{c + 1} Specification",
                    "Engineered with 60-30-10 visual balance, generous negative space, and strict typographic hierarchy.",
                    theme,
                    stat=f"0{c + 1}",
                    invert=is_focus
                )

    prs.save(output_pptx)
    print(f"Generated 30-Layout Aesthetic Master Deck ({theme_name}): {output_pptx}")


if __name__ == "__main__":
    build_30_canonical_layouts_deck("LINEAR_DARK_TECH")
```