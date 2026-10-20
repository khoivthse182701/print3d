---
name: powerpoint_radial_wheel_morph_architect
description: Design and automate interactive Radial Donut Picture Wheel PowerPoint presentations with Boolean Merge Shapes (Subtract/Intersect), Group-Rotate-Ungroup slice expansion, Off-Canvas Pre-Staging prop animations, and 0.75s Morph transitions.
---

# PowerPoint Radial Picture Wheel & Off-Canvas Morph Architect Skill

Use this skill whenever the user asks to create a circular/radial menu presentation, a rotating donut picture wheel showcase, an interactive agenda/portfolio wheel in PowerPoint, or wants to automate radial image slicing and multi-slide Morph choreography.

---

## 1. Core Mechanics & Choreography Rules (The Ppt Pro Method)

### Rule 1: Two-Step Boolean Geometry (`Subtract` then `Intersect`)
1. **Selection Order Matters in PowerPoint `Merge Shapes`:**
   - **To Punch the Donut Hole (`Subtract`):** Convert a 7-segment `Basic Pie` SmartArt to shapes and ungroup. Create 7 concentric inner circles centered on the pie. Hold `Shift`, click the **Pie Wedge FIRST**, click one **Inner Circle SECOND**, then select `Shape Format -> Merge Shapes -> Subtract`. Scale each wedge down slightly (`~95%`) to create clean radial gaps.
   - **To Mask Images into Wedges (`Intersect`):** Send each photo to the back behind its target wedge. Hold `Shift`, click the **Photo FIRST**, click the **Donut Wedge SECOND**, then select `Shape Format -> Merge Shapes -> Intersect`. Fine-tune framing with `Picture Format -> Crop`.

### Rule 2: The `Group -> Rotate -> Ungroup` Morph Law
- PowerPoint's `Morph` engine CANNOT individually expand a single slice if the wheel remains grouped, and rotating ungrouped slices ruins their shared pivot center.
- **Mandatory workflow on every slide:**
  1. Copy the intact 7-slice wheel from the base overview slide.
  2. Select all 7 slices -> `Ctrl + G` (`Group`).
  3. Rotate the group around its center so the active topic slice faces the focal area (e.g., right side of the left-shifted wheel).
  4. **Immediately `Ctrl + Shift + G` (`Ungroup`)** all 7 slices.
  5. Select ONLY the active slice, scale it up (`1.6x–2.2x`), and pull it slightly outward toward the content column.

### Rule 3: Off-Canvas Pre-Staging for Floating Props (Fly-In + Spin)
- Every topic slide features thematic PNG cutout props (e.g., leaves, plates, clouds, vehicles, marine life) layered around the expanded slice and text (`Send to Back` for background clouds/accents, foreground for overlapping props).
- **Pre-Staging Protocol:**
  1. Arrange the props in their final resting positions on `Slide N`.
  2. Copy those exact prop objects (`Ctrl + C`) and paste them onto `Slide N-1` (`Ctrl + V`).
  3. On `Slide N-1`, drag those pasted props **outside the bottom/side slide boundary** (`Off-Canvas`), reduce their size slightly, and rotate them (`45°–90°`, especially for circular items like food plates or wheels).
  4. When transitioning from `Slide N-1` to `Slide N`, Morph automatically interpolates their trajectory, rotation, and scale onto the stage.

### Rule 4: Multi-Slide Narrative Sequence & Timing
- **Slide 1 (Hero Center Wheel):** Full 7-slice wheel centered on canvas with a bold minimal title (`EVENT`) in the hollow center.
- **Slide 2 (Section Overview Transition):** Wheel shifts to the left edge, rotates `~45°`, ungrouped; giant section header (`JUNE`) with a thick horizontal underline bar enters on the right.
- **Slides 3 to 9 (Topic Deep-Dives):** Wheel docked on the left edge; active slice rotates into position and expands outward; right column displays bold Header + `Montserrat` body paragraph + animated floating props.
- **Final Slide (Loop Closure):** Duplicate Slide 1 to the end of the deck.
- **Global Transition Timing:** Select all slides (`Ctrl + A`) -> `Transitions -> Morph` -> Set **`Duration = 0.75s`**.

---

## 2. Automated Python Generator (`Pillow` Radial Donut Slicer + `python-pptx` Builder)

Manually executing 14 `Subtract`/`Intersect` operations and rotating wedges across 8 slides in PowerPoint is time-consuming. When Python is available, run this script to automatically crop any 7 images into anti-aliased radial donut slices and generate the multi-slide PowerPoint deck with pre-rotated slices and off-canvas prop staging:

```python
import os
import math
from PIL import Image, ImageDraw
from pptx import Presentation
from pptx.util import Inches, Pt
from pptx.dml.color import RGBColor


def generate_radial_wedge_png(
    image_path: str,
    output_png_path: str,
    canvas_size: int = 1200,
    outer_radius: int = 560,
    inner_radius: int = 240,
    start_angle_deg: float = 0.0,
    sweep_angle_deg: float = 47.0,
):
    """
    Crops an input image into a high-resolution anti-aliased donut wedge PNG
    (4x supersampled mask) ready for PowerPoint Morph slides.
    """
    scale = 4  # 4x supersampling for razor-sharp curved edges
    hi_size = canvas_size * scale
    center = hi_size // 2

    mask = Image.new("L", (hi_size, hi_size), 0)
    draw = ImageDraw.Draw(mask)

    outer_box = [
        center - outer_radius * scale,
        center - outer_radius * scale,
        center + outer_radius * scale,
        center + outer_radius * scale,
    ]
    inner_box = [
        center - inner_radius * scale,
        center - inner_radius * scale,
        center + inner_radius * scale,
        center + inner_radius * scale,
    ]

    # Draw outer pie wedge and subtract inner circle (Donut Slice)
    draw.pieslice(outer_box, start=start_angle_deg, end=start_angle_deg + sweep_angle_deg, fill=255)
    draw.ellipse(inner_box, fill=0)
    mask = mask.resize((canvas_size, canvas_size), Image.Resampling.LANCZOS)

    # Load and center-crop source image
    if os.path.exists(image_path):
        src = Image.open(image_path).convert("RGBA")
    else:
        src = Image.new("RGBA", (canvas_size, canvas_size), (45, 65, 95, 255))

    w, h = src.size
    min_dim = min(w, h)
    src = src.crop(((w - min_dim) // 2, (h - min_dim) // 2, (w + min_dim) // 2, (h + min_dim) // 2))
    src = src.resize((canvas_size, canvas_size), Image.Resampling.LANCZOS)

    result = Image.new("RGBA", (canvas_size, canvas_size), (0, 0, 0, 0))
    result.paste(src, (0, 0), mask=mask)
    result.save(output_png_path, "PNG")
    return output_png_path


def build_radial_wheel_presentation(
    topics: list[dict],
    output_pptx: str = "Radial_Wheel_Morph_Showcase.pptx",
    work_dir: str = "./wheel_assets"
):
    """
    Builds the complete multi-slide PowerPoint presentation following The Ppt Pro choreography:
    - Slide 1: Centered 7-slice Donut Wheel + 'EVENT' center title
    - Slide 2: Left-docked Wheel + 'JUNE' giant header with underline bar
    - Slides 3+: Rotated wheel with active slice expanded + Montserrat copy + Off-Canvas prop staging
    """
    os.makedirs(work_dir, exist_ok=True)
    prs = Presentation()
    prs.slide_width = Inches(13.333)
    prs.slide_height = Inches(7.5)
    blank_layout = prs.slide_layouts[6]

    num_slices = len(topics)
    step_angle = 360.0 / num_slices
    gap_deg = 4.5
    sweep_deg = step_angle - gap_deg

    # 1. Pre-render base donut slice PNGs at each rotation step
    def add_wheel_to_slide(slide, center_x_in, center_y_in, wheel_diam_in, rot_offset_deg=0.0, active_idx=None):
        for i, topic in enumerate(topics):
            angle = (i * step_angle + rot_offset_deg) % 360.0
            slice_png = os.path.join(work_dir, f"slice_{i}_angle_{int(angle)}.png")
            generate_radial_wedge_png(
                image_path=topic.get("image", ""),
                output_png_path=slice_png,
                start_angle_deg=angle,
                sweep_angle_deg=sweep_deg
            )

            # Expand and offset the active slice when docked on detail slides
            is_active = (i == active_idx)
            diam = wheel_diam_in * (1.28 if is_active else 1.0)
            offset_x = Inches(0.45) if is_active else Inches(0.0)

            left = Inches(center_x_in - diam / 2.0) + offset_x
            top = Inches(center_y_in - diam / 2.0)
            pic = slide.shapes.add_picture(slice_png, left, top, width=Inches(diam), height=Inches(diam))
            # Match shape name across slides (!! prefix) so PowerPoint Morph tracks each slice smoothly
            pic.name = f"!!Wheel_Slice_{i:02d}"

    # --- SLIDE 1: Hero Centered Wheel ---
    s1 = prs.slides.add_slide(blank_layout)
    add_wheel_to_slide(s1, center_x_in=6.666, center_y_in=3.75, wheel_diam_in=5.8, rot_offset_deg=0.0)
    tb1 = s1.shapes.add_textbox(Inches(5.166), Inches(3.25), Inches(3.0), Inches(1.0))
    tb1.name = "!!Event_Label"
    p1 = tb1.text_frame.paragraphs[0]
    p1.text = "EVENT"
    p1.font.size = Pt(36)
    p1.font.bold = True
    p1.font.name = "Montserrat"

    # --- SLIDE 2: Left-Shifted Wheel + Giant Section Title ("JUNE") ---
    s2 = prs.slides.add_slide(blank_layout)
    add_wheel_to_slide(s2, center_x_in=2.8, center_y_in=3.75, wheel_diam_in=5.4, rot_offset_deg=25.0)
    tb2 = s2.shapes.add_textbox(Inches(6.2), Inches(2.2), Inches(6.0), Inches(2.5))
    p2 = tb2.text_frame.paragraphs[0]
    p2.text = "JUNE"
    p2.font.size = Pt(110)
    p2.font.bold = True
    p2.font.name = "Montserrat"

    # --- SLIDES 3+: Individual Slice Deep-Dives ---
    for idx, topic in enumerate(topics):
        s_topic = prs.slides.add_slide(blank_layout)
        # Rotate wheel so slice `idx` lands at 0 degrees (facing right toward the text column)
        target_rot = (-idx * step_angle - sweep_deg / 2.0) % 360.0
        add_wheel_to_slide(
            s_topic,
            center_x_in=1.6,
            center_y_in=3.75,
            wheel_diam_in=5.6,
            rot_offset_deg=target_rot,
            active_idx=idx
        )

        # Topic Title & Montserrat Body Text
        tb_title = s_topic.shapes.add_textbox(Inches(5.6), Inches(1.8), Inches(6.8), Inches(1.2))
        tb_title.name = "!!Topic_Header"
        pt = tb_title.text_frame.paragraphs[0]
        pt.text = topic["title"].upper()
        pt.font.size = Pt(48)
        pt.font.bold = True
        pt.font.name = "Montserrat"

        tb_body = s_topic.shapes.add_textbox(Inches(5.6), Inches(3.1), Inches(6.2), Inches(2.5))
        tb_body.name = "!!Topic_Body"
        tb_body.text_frame.word_wrap = True
        pb = tb_body.text_frame.paragraphs[0]
        pb.text = topic["description"]
        pb.font.size = Pt(16)
        pb.font.name = "Montserrat"
        pb.font.color.rgb = RGBColor(90, 95, 105)

    prs.save(output_pptx)
    print(f"Saved Radial Wheel Morph presentation to: {output_pptx}")
```

---

## 3. Troubleshooting Checklist for Users

1. **"My slices deform weirdly or flip inside-out during Morph!"**
   - Make sure the slices are **Ungrouped** on both the starting slide and destination slide, and ensure each slice retains its matching Selection Pane name (`!!Wheel_Slice_01`, `!!Wheel_Slice_02`, etc.).
2. **"When I click `Merge Shapes -> Subtract` or `Intersect`, the photo disappears or turns solid blue!"**
   - You selected the objects in the wrong order.
   - For **`Subtract`**: Click **Pie Wedge FIRST** $\rightarrow$ Hold `Shift` $\rightarrow$ Click **Circle SECOND**.
   - For **`Intersect`**: Click **Photo FIRST** $\rightarrow$ Hold `Shift` $\rightarrow$ Click **Wedge SECOND**.
3. **"My decorative props just fade in instead of flying in and spinning!"**
   - You forgot to paste a copy of those props onto the **previous slide** outside the bottom canvas edge (`Off-Canvas Pre-Staging`).