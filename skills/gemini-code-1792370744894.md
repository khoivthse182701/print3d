---
name: powerpoint_30_transitions_master
description: Master choreography catalog and automated python-pptx OpenXML engine for building professional presentations with 30 distinct slide transitions (Morph geometry, multi-layer parallax, kinetic typography, 3D model rotation, and native OpenXML transition injection).
---

# 30-Transition Slide Master & OpenXML Choreography Skill

Use this skill whenever the user asks to design a high-impact presentation deck, apply diverse slide transitions, avoid repetitive slide layouts, or programmatically generate `.pptx` files with 30 cinematic transitions using `python-pptx` and OpenXML.

---

## 1. The Golden Rules of Slide Transition Choreography

1. **The `!!` Morph Pairing Law:**
   - For any Morph-based transition (Transitions `#01` to `#24`), matching objects across consecutive slides (`Slide N` and `Slide N+1`) MUST share the exact same name prefixed with double exclamation marks in the Selection Pane (e.g., `!!Hero_Shape`, `!!Bg_Layer_01`, `!!Active_Pill`).
2. **Off-Canvas Pre-Staging & Post-Staging:**
   - Never let elements pop into existence abruptly. Place incoming elements **outside the slide boundary** on `Slide N` (`Off-Canvas`), bring them onto the stage on `Slide N+1`, and push outgoing elements off-canvas on `Slide N+1`.
3. **Pacing & Duration Standards:**
   - **Snappy UI / Bento / Split-Screen Morphs:** `0.60s – 0.75s` (`spd="fast"`, `advTm` or `p14:dur="750"`).
   - **Cinematic Parallax / 3D Flip / Camera Pan Morphs:** `1.10s – 1.50s` (`p14:dur="1250"`).
   - **Kinetic Character / Word Morphs:** `0.85s – 1.00s` (`p159:morph option="byChar"` or `"byWord"`).

---

## 2. Complete Catalog of 30 Slide Transitions (5 Core Categories)

When designing or generating a multi-slide presentation, select transitions from these 30 blueprints to match the narrative intent of each slide:

### Group A: Geometric & Masking Morphs (`#01` – `#06`)
| ID | Transition Name | Mechanics (`Slide N` $\rightarrow$ `Slide N+1`) | Best Use Case |
| :--- | :--- | :--- | :--- |
| **01** | **Circle Iris Expand** | Small circle button (`!!Iris_Mask`) on Slide N scales up to `25x` covering the entire canvas on Slide N+1 while revealing new content inside. | Opening a new chapter or diving into a core concept. |
| **02** | **Diagonal Curtain Slash** | Two large right-triangle shapes (`!!Slash_Top`, `!!Slash_Bottom`) slide in from opposite corners to slice the screen diagonally, then part ways. | High-energy product reveal or vs. comparison. |
| **03** | **Bento Grid Zoom-In** | A 4-or-6 card Bento Grid where one target card (`!!Bento_Focus`) expands to full screen (`13.33" x 7.5"`) while sibling cards push off-canvas outward. | Architecture modules, feature breakdowns. |
| **04** | **Pill Accordion Slider** | 4 vertical rounded-pill strips (`!!Pill_01..04`); active pill widens from `1.5"` to `8.0"` horizontally while others compress into side tabs. | Step-by-step process, 4-stage timeline. |
| **05** | **Radial Donut Wheel Spin** | 7-slice hollow donut ring rotated around a shared center (`!!Wheel_Slice_01..07`); active wedge scales out by `1.3x` toward the text column. | Agenda overview, multi-topic portfolio. |
| **06** | **Polygon Mosaic Assemble** | 6–8 angled shapes scattered off-canvas on Slide N fly inward and lock together seamlessly like a puzzle on Slide N+1. | Team synergy, system integration overview. |

### Group B: Multi-Layer Parallax & Camera Rigging (`#07` – `#12`)
| ID | Transition Name | Mechanics (`Slide N` $\rightarrow$ `Slide N+1`) | Best Use Case |
| :--- | :--- | :--- | :--- |
| **07** | **3-Layer Z-Depth Pan** | Background (`!!Bg`) moves `-1.5"`, Midground (`!!Mid`) moves `-4.0"`, Foreground (`!!Fg`) moves `-9.0"` horizontally to simulate 3D camera panning. | Storytelling timelines, geographical journeys. |
| **08** | **Vertical Cliff/Foliage Mask** | A dark foreground silhouette (`!!Fg_Blocker`) sweeps from bottom (`Y = +8.5"`) to top (`Y = -8.5"`) masking the background swap underneath. | Seamless scene changes (Nohosan style). |
| **09** | **Off-Canvas Prop Fly & Spin** | Cutout PNG props (`!!Prop_01..03`) staged below the bottom edge rotated `-60°` fly up into place and rotate to `0°`. | Food, e-commerce products, lifestyle slides. |
| **10** | **Infinite Canvas Camera Dolly** | Giant grouped map/diagram (`26" x 15"`) shifts `(X, Y)` coordinates and scales (`1.0x -> 1.8x`) to simulate a roving camera lens over a blueprint. | System architecture ERD, business model canvas. |
| **11** | **Glassmorphism Card Shuffle** | 3 stacked semi-transparent cards (`!!Glass_01..03`); front card slides down/fades out while middle card scales up (`0.9x -> 1.0x`) to take the front slot. | Testimonials, case studies, KPI metrics. |
| **12** | **Tunnel Zoom Through Hollow Text** | Giant bold title at center scales up to `400pt+` and moves off-screen while the background image scales from `0.6x` to `1.0x` through the letters. | Title slide to Slide 2 dramatic opening. |

### Group C: Kinetic Typography & Split-Screen Morphs (`#13` – `#18`)
| ID | Transition Name | Mechanics (`Slide N` $\rightarrow$ `Slide N+1`) | Best Use Case |
| :--- | :--- | :--- | :--- |
| **13** | **Character Anagram Scramble** | Enable `Morph: Characters` (`option="byChar"`). Shared letters between Slide N's headline and Slide N+1's headline physically fly and rearrange. | Slogans, key takeaways, chapter titles. |
| **14** | **Word Cascade Reflow** | Enable `Morph: Words` (`option="byWord"`). Keywords from a paragraph rearrange themselves into bullet headers on the next slide. | Problem statement to solution pillars. |
| **15** | **Counter-Sliding Split Screen** | Left 50% image panel (`!!Split_L`) slides down from top while Right 50% text panel (`!!Split_R`) slides up from bottom, swapping sides on the next slide. | Before/After, Pros/Cons, UI vs. Backend. |
| **16** | **Vertical Odometer Number Roll** | A vertical strip of numbers (`01 / 02 / 03 / 04`) masked inside a window shifts up by exact line height so the counter rolls like an odometer. | Numbered KPIs, financial highlights. |
| **17** | **Floating Nav Pill Follower** | Top/bottom navigation bar with a colored capsule (`!!Nav_Active_Pill`) that glides smoothly horizontally from Tab 1 to Tab 2, Tab 3, etc. | Keeping audience oriented across long decks. |
| **18** | **Header-to-Sidebar Dock** | Centered hero title (`!!Section_Title`, `64pt`) shrinks to `20pt`, rotates `-90°`, and docks cleanly along the left vertical margin. | Transitioning from section cover to data slide. |

### Group D: 3D Object & Perspective Choreography (`#19` – `#24`)
| ID | Transition Name | Mechanics (`Slide N` $\rightarrow$ `Slide N+1`) | Best Use Case |
| :--- | :--- | :--- | :--- |
| **19** | **3D Card 180° Flip (`.glb`)** | Double-sided `.glb` card (`!!Card_3D`) rotates `180°` around the Y-axis while scaling up to reveal the blurred info back-face. | Team member profiles, location cards. |
| **20** | **3D Turntable Orbit + Callouts** | Central 3D model rotates `60°` on Y-axis and `15°` on X-axis while leader-line callout boxes (`!!Callout_1..3`) reposition around key parts. | Hardware, 3D character showcase, product specs. |
| **21** | **Isometric 3D Layer Explode** | 3 stacked isometric planes (`!!Iso_Top`, `!!Iso_Mid`, `!!Iso_Base`) separate vertically along the Y-axis with connecting dashed lines appearing. | Clean Architecture layers, tech stack diagram. |
| **22** | **Perspective CoverFlow Carousel** | 5 cards in a row; side cards have 3D perspective rotation (`Y = ±35°`), center card has `Y = 0°` and `1.3x` scale; shifting index slides the carousel. | Portfolio gallery, screen mockups. |
| **23** | **3D Cube Room Corner Turn** | Two wall panels joined at a perspective seam shift angles (`-30° -> +30°`) simulating a 90° camera turn inside a 3D gallery room. | Virtual exhibition, milestone showcase. |
| **24** | **3D Book / Folder Unfold** | Left cover plane flips `-160°` horizontally while right page slides to center, simulating a physical dossier opening on screen. | Case study report, academic proposal. |

### Group E: Native OpenXML Engine Transitions (`#25` – `#30`)
| ID | Transition Name | OpenXML Tag (`p` / `p14` / `p15`) | Best Use Case |
| :--- | :--- | :--- | :--- |
| **25** | **Directional Push (`Push`)** | `<p:push dir="u"/>` (`u`, `d`, `l`, `r`) | Vertical continuous scroll simulation (`dir="u"`). |
| **26** | **Optical Warp Zoom (`Zoom`)** | `<p:zoom dir="in"/>` (`in` or `out`) | Diving from macro overview to micro detail. |
| **27** | **Diagonal Strips (`Strips`)** | `<p:strips dir="ld"/>` (`lu`, `ld`, `ru`, `rd`) | Clean geometric wipe without extra shapes. |
| **28** | **Water Ripple (`Ripple`)** | `<p14:ripple centerPos="..."/>` | Emotional storytelling or calm transition. |
| **29** | **Hexagonal Honeycomb** | `<p14:honeycomb/>` | Tech, data grid, or sci-fi themed slide change. |
| **30** | **Particle Vortex (`Vortex`)** | `<p14:vortex dir="l"/>` | Grand finale or high-impact dramatic reveal. |

---

## 3. Reference Python Implementation (`python-pptx` + OpenXML Transition Engine)

Because `python-pptx` does not include built-in methods for `<p:transition>`, ALWAYS use this OpenXML injector to programmatically apply any of the 30 transitions (including `Morph: Objects`, `Morph: Words`, `Morph: Characters`, and Office 2010/2019 `p14`/`p159` transitions):

```python
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor
from pptx.enum.shapes import MSO_SHAPE
from pptx.oxml import parse_xml
from pptx.oxml.ns import nsdecls


def apply_openxml_transition(
    slide,
    transition_type: str = "morph",
    duration_ms: int = 1000,
    direction: str = "u",
    morph_option: str = "byObject"
):
    """
    Injects native OpenXML <p:transition> into a python-pptx slide.
    Supports:
      - 'morph' (with morph_option: 'byObject', 'byWord', 'byChar')
      - 'push' (dir: 'u', 'd', 'l', 'r')
      - 'zoom' (dir: 'in', 'out')
      - 'strips' (dir: 'ld', 'lu', 'rd', 'ru')
      - 'fade', 'wipe', 'split'
      - 'ripple', 'honeycomb', 'vortex', 'glitter', 'shred', 'prism' (Office p14 extensions)
    """
    sld = slide._element
    # Remove any existing transition element first
    for existing in sld.findall("{[http://schemas.openxmlformats.org/presentationml/2006/main](http://schemas.openxmlformats.org/presentationml/2006/main)}transition"):
        sld.remove(existing)

    t_type = transition_type.lower()

    if t_type == "morph":
        # Office 2019 / 365 Morph Transition (p159 namespace)
        xml_str = f"""
        <p:transition {nsdecls('p')} xmlns:p14="[http://schemas.microsoft.com/office/powerpoint/2010/main](http://schemas.microsoft.com/office/powerpoint/2010/main)"
                      xmlns:p159="[http://schemas.microsoft.com/office/powerpoint/2015/09/main](http://schemas.microsoft.com/office/powerpoint/2015/09/main)"
                      spd="med" p14:dur="{duration_ms}">
          <p:extLst>
            <p:ext uri="{{D9430332-B6B8-4315-8B28-04E9A744B0FE}}">
              <p159:morph option="{morph_option}"/>
            </p:ext>
          </p:extLst>
        </p:transition>
        """
    elif t_type in {"ripple", "honeycomb", "vortex", "glitter", "shred", "prism"}:
        # Office 2010+ p14 High-Impact Transitions
        inner_attr = f' dir="{direction}"' if t_type in {"vortex", "glitter"} else ""
        xml_str = f"""
        <p:transition {nsdecls('p')} xmlns:p14="[http://schemas.microsoft.com/office/powerpoint/2010/main](http://schemas.microsoft.com/office/powerpoint/2010/main)"
                      spd="med" p14:dur="{duration_ms}">
          <p:extLst>
            <p:ext uri="{{CE6CE671-F284-4235-B8B7-4F3F06D5A82C}}">
              <p14:{t_type}{inner_attr}/>
            </p:ext>
          </p:extLst>
        </p:transition>
        """
    elif t_type in {"push", "wipe", "strips"}:
        xml_str = f"""
        <p:transition {nsdecls('p')} xmlns:p14="[http://schemas.microsoft.com/office/powerpoint/2010/main](http://schemas.microsoft.com/office/powerpoint/2010/main)"
                      spd="med" p14:dur="{duration_ms}">
          <p:{t_type} dir="{direction}"/>
        </p:transition>
        """
    elif t_type == "zoom":
        zoom_dir = "in" if direction not in {"in", "out"} else direction
        xml_str = f"""
        <p:transition {nsdecls('p')} xmlns:p14="[http://schemas.microsoft.com/office/powerpoint/2010/main](http://schemas.microsoft.com/office/powerpoint/2010/main)"
                      spd="med" p14:dur="{duration_ms}">
          <p:zoom dir="{zoom_dir}"/>
        </p:transition>
        """
    else:
        # Default Smooth Fade
        xml_str = f"""
        <p:transition {nsdecls('p')} xmlns:p14="[http://schemas.microsoft.com/office/powerpoint/2010/main](http://schemas.microsoft.com/office/powerpoint/2010/main)"
                      spd="med" p14:dur="{duration_ms}">
          <p:fade thruBlk="0"/>
        </p:transition>
        """

    sld.append(parse_xml(xml_str))


def add_named_morph_shape(slide, shape_type, name, left, top, width, height, fill_rgb, text="", font_pt=24, rotation=0):
    """Creates a shape with a strict '!!' Morph tracking name, fill color, and rotation."""
    shape = slide.shapes.add_shape(shape_type, Inches(left), Inches(top), Inches(width), Inches(height))
    shape.name = name if name.startswith("!!") else f"!!{name}"
    shape.rotation = rotation
    shape.fill.solid()
    shape.fill.fore_color.rgb = RGBColor(*fill_rgb)
    shape.line.fill.background()

    if text:
        tf = shape.text_frame
        tf.word_wrap = True
        p = tf.paragraphs[0]
        p.text = text
        p.font.size = Pt(font_pt)
        p.font.bold = True
        p.font.name = "Montserrat"
        p.font.color.rgb = RGBColor(255, 255, 255)
    return shape


# ==============================================================================
# DEMO GENERATOR: Generates a Deck Demonstrating All 30 Transition Blueprints
# ==============================================================================
def generate_30_transitions_master_deck(output_path: str = "Master_30_Transitions_Deck.pptx"):
    prs = Presentation()
    prs.slide_width = Inches(13.333)
    prs.slide_height = Inches(7.5)
    blank = prs.slide_layouts[6]

    catalog = [
        # Group A: Geometric & Masking Morphs (01-06)
        ("01. Circle Iris Expand", "morph", "byObject", "u", 900),
        ("02. Diagonal Curtain Slash", "morph", "byObject", "u", 850),
        ("03. Bento Grid Zoom-In", "morph", "byObject", "u", 800),
        ("04. Pill Accordion Slider", "morph", "byObject", "u", 750),
        ("05. Radial Donut Wheel Spin", "morph", "byObject", "u", 750),
        ("06. Polygon Mosaic Assemble", "morph", "byObject", "u", 950),
        # Group B: Parallax & Camera Rigging (07-12)
        ("07. 3-Layer Z-Depth Pan", "morph", "byObject", "u", 1200),
        ("08. Vertical Cliff Drop Mask", "morph", "byObject", "u", 1200),
        ("09. Off-Canvas Prop Fly & Spin", "morph", "byObject", "u", 900),
        ("10. Infinite Canvas Camera Dolly", "morph", "byObject", "u", 1250),
        ("11. Glassmorphism Card Shuffle", "morph", "byObject", "u", 850),
        ("12. Tunnel Zoom Through Text", "morph", "byObject", "u", 1100),
        # Group C: Kinetic Typography & Split-Screen (13-18)
        ("13. Character Anagram Scramble", "morph", "byChar", "u", 950),
        ("14. Word Cascade Reflow", "morph", "byWord", "u", 900),
        ("15. Counter-Sliding Split Screen", "morph", "byObject", "u", 800),
        ("16. Vertical Odometer Number Roll", "morph", "byObject", "u", 750),
        ("17. Floating Nav Pill Follower", "morph", "byObject", "u", 650),
        ("18. Header-to-Sidebar Dock", "morph", "byObject", "u", 850),
        # Group D: 3D & Perspective Choreography (19-24)
        ("19. 3D Card 180 Flip", "morph", "byObject", "u", 1200),
        ("20. 3D Turntable Orbit + Callouts", "morph", "byObject", "u", 1200),
        ("21. Isometric 3D Layer Explode", "morph", "byObject", "u", 1000),
        ("22. Perspective CoverFlow Carousel", "morph", "byObject", "u", 850),
        ("23. 3D Cube Room Corner Turn", "morph", "byObject", "u", 1100),
        ("24. 3D Book Dossier Unfold", "morph", "byObject", "u", 1000),
        # Group E: Native OpenXML Engine Transitions (25-30)
        ("25. Native Directional Push", "push", "byObject", "u", 800),
        ("26. Native Optical Warp Zoom", "zoom", "byObject", "in", 900),
        ("27. Native Diagonal Strips", "strips", "byObject", "ld", 850),
        ("28. Native Water Ripple", "ripple", "byObject", "u", 1100),
        ("29. Native Hexagonal Honeycomb", "honeycomb", "byObject", "u", 1200),
        ("30. Native Particle Vortex", "vortex", "byObject", "l", 1300),
    ]

    palette = [
        (15, 23, 42), (30, 41, 59), (17, 24, 39),
        (24, 24, 27), (9, 13, 26), (23, 37, 84)
    ]

    for idx, (title, t_type, m_opt, direction, dur) in enumerate(catalog):
        slide = prs.slides.add_slide(blank)
        bg_color = palette[idx % len(palette)]

        # 1. Persistent Morph Background Canvas
        add_named_morph_shape(slide, MSO_SHAPE.RECTANGLE, "!!Canvas_Bg", 0, 0, 13.333, 7.5, bg_color)

        # 2. Dynamic Hero Geometry that transforms position, size, and angle across all 30 slides
        phase = idx % 6
        hero_positions = [
            (0.8, 1.2, 4.5, 5.0, 0),
            (7.8, 1.2, 4.5, 5.0, 15),
            (4.4, 0.8, 4.5, 3.0, -10),
            (1.0, 4.2, 11.3, 2.4, 0),
            (8.5, 0.8, 4.0, 5.8, 45),
            (0.8, 0.8, 5.5, 5.8, -8),
        ]
        hx, hy, hw, hh, hrot = hero_positions[phase]
        add_named_morph_shape(
            slide, MSO_SHAPE.ROUNDED_RECTANGLE, "!!Morph_Hero_Card",
            hx, hy, hw, hh, (56, 189, 248), rotation=hrot
        )

        # 3. Floating Accent Orb (Off-Canvas to On-Canvas motion)
        orb_x = 11.2 if idx % 2 == 0 else 1.2
        orb_y = 1.0 if idx % 3 == 0 else 5.2
        add_named_morph_shape(
            slide, MSO_SHAPE.OVAL, "!!Morph_Accent_Orb",
            orb_x, orb_y, 1.4, 1.4, (245, 158, 11)
        )

        # 4. Floating Navigation Pill Tracker (Transition #17 embedded across deck)
        pill_x = 0.8 + (idx / 30.0) * 10.5
        add_named_morph_shape(
            slide, MSO_SHAPE.ROUNDED_RECTANGLE, "!!Nav_Progress_Pill",
            pill_x, 6.85, 1.2, 0.25, (16, 185, 129)
        )

        # 5. Kinetic Title Box (Supports byObject, byWord, and byChar Morphing)
        title_x = 1.2 if hx > 4.0 else 6.0
        add_named_morph_shape(
            slide, MSO_SHAPE.RECTANGLE, "!!Slide_Headline",
            title_x, 2.4, 6.2, 2.2, bg_color,
            text=title, font_pt=40
        )

        # 6. Inject the exact OpenXML Transition into the Slide XML
        apply_openxml_transition(
            slide,
            transition_type=t_type,
            duration_ms=dur,
            direction=direction,
            morph_option=m_opt
        )

    prs.save(output_path)
    print(f"Generated 30-Transition Master Presentation: {output_path}")


if __name__ == "__main__":
    generate_30_transitions_master_deck()
```