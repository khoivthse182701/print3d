---
name: powerpoint_3d_morph_card_designer
description: Design interactive 3D flip-card PowerPoint presentations using AI-generated visuals (Leonardo.ai), two-sided rounded card templates (Front visual + Blurred Back info layout), 3D model conversion (Paint 3D / Vectary / automated Python GLB generator), and seamless 3D Morph transitions.
---

# PowerPoint 3D Flip-Card & Morph Transition Designer Skill

Use this skill whenever the user asks to design a 3D PowerPoint presentation, create 3D flipping cards for team member introductions, showcase locations/products with two-sided 3D cards, or automate 3D `.glb` card models for PowerPoint `Morph` transitions.

---

## 1. Prerequisites & Core Design Architecture

1. **PowerPoint Version Requirement:**
   - Must use **PowerPoint 2019, 2021, or Microsoft 365** (`Insert -> 3D Models` and `Insert -> Icons` support + `Transitions -> Morph`).
2. **Visual Hierarchy & Contrast Principle:**
   - **Slide Background (16:9):** Always apply `Artistic Effects -> Blur` + `Corrections -> Decrease Brightness (-20% to -40%)` so foreground 3D cards pop with high visual contrast.
   - **Card Front Face (`front.png`):** Clean artwork/portrait cropped to a `Rounded Rectangle` with a crisp `1.5 pt` white border (`#FFFFFF`).
   - **Card Back Face (`back.png`):** Duplicated front shape with `Blur` + darkened brightness, overlaid with:
     - Bold white header title.
     - Rounded inner container (`White Outline 1pt`, `Black Fill` with `40%–60% Transparency`) containing centered description text.
     - Minimalist white vector `Icon` anchored at the bottom.

---

## 2. Standard 5-Stage Production Workflow

### Stage 1: AI Asset Generation (Leonardo.ai / Midjourney / Nano Banana)
1. **Slide Background Prompting:**
   - Aspect Ratio: `16:9` (`1280x720` or `1920x1080`).
   - Style consistency: Keep the same model/preset (e.g., `Anime`, `Concept Art`, or `Cinematic ArchViz`) across the background and all card subjects.
2. **Card Subject Prompting:**
   - Generate 4 distinct character portraits, landmarks, or product shots matching the presentation theme.

### Stage 2: Two-Sided Card Authoring in PowerPoint
1. **Front Face Construction:**
   - Insert character/location image -> `Picture Format -> Crop -> Crop to Shape -> Rounded Rectangle`.
   - Pull the yellow adjustment handle inward for smooth corner radius.
   - Set border: `Solid Line`, Color `White`, Width `1.5 pt`.
   - Right-click -> `Save as Picture...` -> Save as **`card_01_front.png`**.
2. **Back Face Construction:**
   - Duplicate the front card (`Ctrl + D`).
   - Apply `Picture Format -> Artistic Effects -> Blur` and `Corrections -> Brightness -30%`.
   - Add Title TextBox (White, Bold, centered at top).
   - Draw inner Rounded Rectangle (`Fill: Black, Transparency: 50%`, `Outline: White, 1 pt`) and insert concise body text.
   - Insert thematic vector icon (`Insert -> Icons`), set `Graphics Fill = White`, place at bottom center.
   - Select all Back Face elements -> Right-click -> `Save as Picture...` -> Save as **`card_01_back.png`**.

### Stage 3: Converting Front & Back PNGs into a Single 3D Model (`.glb`)

Choose between the **No-Code Paint 3D Method** or the **High-Resolution Python `.glb` Generator Method**:

#### Method A — Windows Paint 3D (No-Code Free Workflow)
1. Open **Paint 3D** -> `New` -> `Menu -> Insert` -> Select `card_01_front.png` -> Click **`Make 3D`**.
2. Go to `Menu -> Insert` -> Select `card_01_back.png` -> Hold `Shift` to scale proportionally -> Click **`Make 3D`** -> Match exact width and height of the front card.
3. **CRITICAL STEP:** With the back card selected, click **`Flip Horizontal`** so the back text reads correctly when rotated 180 degrees in 3D space.
4. Align both cards directly on top of each other, then drag the **Z-axis depth handle** slightly forward/backward (`~1–2mm` separation) so the two planes do not Z-fight or clip through one another.
5. Drag-select both cards -> Click **`Group`** -> `Menu -> Save as -> 3D Model (.glb)`.

#### Method B — Automated High-Resolution Python `.glb` Card Generator (Zero Blurry Edges)
Paint 3D downscales textures and can cause jagged edges. Whenever Python or Blender (`bpy`) is available on the user's machine, run the script in **Section 3** below to generate a crisp, double-sided `.glb` 3D card with preserved PNG alpha transparency and zero resolution loss.

### Stage 4: Multi-Slide Choreography & `Morph` Transition Setup
1. **Slide 1 (Overview Grid / Fan Layout):**
   - Insert all 4 `.glb` 3D cards (`Insert -> 3D Models -> This Device`).
   - Arrange the 4 cards horizontally across the blurred background, tilted at subtle 3D angles (`Y-rotation: -15° to +15°`) showing their **Front Faces**.
2. **Slide 2+ (Focused Card Flip & Inspection):**
   - Duplicate Slide 1 (`Ctrl + D`).
   - Select the target 3D card -> Use the central 3D rotation gizmo (or `3D Model Format -> 3D Rotation`) to rotate **180° around the Y-axis** (revealing the **Back Face** with description text), scale it up in the center/focus area, and push the remaining 3 cards slightly to the side or background.
   - Go to `Transitions -> Morph` (Duration: `1.25s – 1.50s`).
3. **Rule for Seamless Morphing:**
   - Never delete and re-insert the 3D model on the next slide; always duplicate the slide or copy-paste the exact instance (or use the `!!card_01` naming convention in the Selection Pane) so PowerPoint's Morph engine tracks the 3D rotation smoothly.

---

## 3. Automated Double-Sided 3D Card `.glb` Generator (`bpy` Script)

When the user wants sharp, uncompressed 3D cards without Paint 3D's aliasing or Vectary's paywall, run this headless Blender Python script (`blender -b -P generate_3d_card.py -- front.png back.png card_01.glb`):

```python
import bpy
import sys
import os
import math


def create_double_sided_ppt_card_glb(
    front_png_path: str,
    back_png_path: str,
    output_glb_path: str,
    card_width: float = 0.65,
    card_height: float = 1.0,
    z_separation: float = 0.004
):
    """
    Generates a crisp, double-sided 3D card (.glb) from front.png and back.png
    ready for PowerPoint Insert -> 3D Models and Morph transitions.
    Automatically flips the back UV horizontally so text reads properly when rotated 180 deg.
    """
    bpy.ops.wm.read_factory_settings(use_empty=True)

    def build_textured_plane(name: str, img_path: str, z_pos: float, rot_y: float):
        bpy.ops.mesh.primitive_plane_add(size=1.0, location=(0.0, 0.0, z_pos))
        obj = bpy.context.active_object
        obj.name = name
        obj.scale = (card_width, card_height, 1.0)
        obj.rotation_euler = (math.radians(90.0), rot_y, 0.0)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

        mat = bpy.data.materials.new(name=f"{name}_Mat")
        mat.use_nodes = True
        mat.blend_method = 'CLIP'
        nodes = mat.node_tree.nodes
        links = mat.node_tree.links

        bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
        bsdf.inputs['Roughness'].default_value = 0.25
        if 'Specular IOR Level' in bsdf.inputs:
            bsdf.inputs['Specular IOR Level'].default_value = 0.3

        tex = nodes.new(type='ShaderNodeTexImage')
        if os.path.exists(img_path):
            tex.image = bpy.data.images.load(os.path.abspath(img_path))

        links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
        links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
        obj.data.materials.append(mat)
        return obj

    # Front Face (+Z) and Back Face (-Z, rotated 180 deg around Y so text isn't mirrored)
    front_obj = build_textured_plane("Card_Front", front_png_path, z_pos=z_separation / 2.0, rot_y=0.0)
    back_obj = build_textured_plane("Card_Back", back_png_path, z_pos=-z_separation / 2.0, rot_y=math.radians(180.0))

    # Parent/Join into a single clean 3D object for PowerPoint
    bpy.ops.object.select_all(action='DESELECT')
    front_obj.select_set(True)
    back_obj.select_set(True)
    bpy.context.view_layer.objects.active = front_