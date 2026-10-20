---
name: architectural_environment_production
description: End-to-end procedural 3D architectural environment generation, modular kitbashing, shader imperfection stacking, and mandatory studio photorealism polish in Blender using Python (bpy).
---

# Architectural Environment Production Skill (Blender `bpy`)

Use this skill whenever the user asks to generate, build, light, or render a 3D architectural scene, temple, interior/exterior environment, or photorealistic ArchViz diorama in Blender.

## 1. Scene Setup & Render Engine Standards
- **Render Engine:** Always set `bpy.context.scene.render.engine = 'CYCLES'` with GPU compute enabled if available, `use_adaptive_sampling = True`, and `Color Management` set to `AgX` (or `Filmic` on older Blender versions) with `Medium High Contrast` look.
- **Scale & Units:** Enforce metric units (`unit_settings.system = 'METRIC'`, `scale_length = 1.0`). Build all architectural elements (columns, roofs, beams, stairs, lanterns, foliage) to real-world human scale.
- **Modular Hierarchy:** Organize objects cleanly into collections (`01_Architecture`, `02_Props_Foliage`, `03_Lighting_Gobos`, `04_Cameras_Targets`, `05_Atmosphere`).

## 2. Procedural Geometry & Architectural Kitbashing
- Never represent complex buildings with single primitive blocks. Construct layered modular assemblies:
  - Stepped stone foundations, chamfered timber pillars, brackets/joinery, curved tiled roofs with ridge ornaments, railings, and terrain displacement.
- Apply `Bevel` modifiers (`width = 0.003–0.008`, `segments = 2–3`) to hard architectural meshes so sharp corners catch specular highlights naturally.

## 3. Multi-Camera Framing & Composition
- Create both wide establishing architectural cameras (`Cam_01` to `Cam_05`, focal length `24mm–35mm`) and medium/close detail cameras (`Cam_06` to `Cam_13`, focal length `50mm–85mm`).
- Always verify camera placement and framing against target subjects before final rendering.

## 4. Five Blender-Native Visual Strengths for Breaking Digital Perfection (Mandatory Studio Polish)

Perfect computer-generated geometry looks inherently fake. To achieve true studio photorealism, every build MUST execute these five Blender-native optical and physical imperfection layers:

1. **Gobos & Procedural Light Texturing (Breaking Up Uniform Light):**
   - **Gobos (Go-Betweens):** Place an overhead plane (`Light_Gobo_Canopy`) with a black-and-white alpha-cutout procedural foliage/slat shader (`ShaderNodeTexVoronoi` / `ShaderNodeTexNoise` -> `ShaderNodeBsdfTransparent` mixed with `ShaderNodeBsdfDiffuse`) directly in front of primary directional/spot lights to cast dappled real-world light occlusion across floors and walls.
   - **Procedural Light Textures:** Enable `light.use_nodes = True` on all Area/Spot lights and wire a `ShaderNodeTexNoise` (`Scale = 6.0`) -> `ShaderNodeValToRGB` (`0.65–1.0`) into the light's `Emission` color/strength so light intensity fluctuates naturally across surfaces.

2. **High-Frequency Micro-Roughness & Pointiness/AO Imperfection Stacking:**
   - **Micro-Roughness Stacking:** Never use a single flat roughness map. Stack the base Nano Banana roughness texture with a secondary procedural noise map using a `ShaderNodeMix` set to `LINEAR_LIGHT` (`Factor = 0.20`) so surfaces catch specular glints naturally.
   - **Geometry Procedural Edge-Wear:** Plug `ShaderNodeNewGeometry.outputs['Pointiness']` and `ShaderNodeAmbientOcclusion` through a `ShaderNodeValToRGB` into `Base Color` and `Roughness` so Blender automatically detects sharp edges and crevices to add subtle edge-wear and crevice dirt where objects meet.

3. **Physical Camera Calibration (Full-Frame 35mm Sensor & True Depth of Field (`f/1.8–f/2.8`)):**
   - **Full-Frame 35mm Sensor Calibration:** Set `cam.data.sensor_fit = 'HORIZONTAL'` and `cam.data.sensor_width = 36.0` (Full-Frame 35mm cinema/ArchViz sensor) across all cameras so focal compression matches real cinematography rather than default digital perspective lines.
   - **True Depth of Field (`Camera_Focus_Target`):** Enable `cam.data.dof.use_dof = True`, bind `cam.data.dof.focus_object` to a dedicated `Focus_Target_<CamName>` Empty placed on the primary subject, and set `cam.data.dof.aperture_fstop` to `1.8–2.8` for close/medium shots (`Cam_06`, `Cam_10–Cam_13`) and `4.0–5.6` for wide architectural shots (`Cam_01–Cam_05`) to force soft, cinematic bokeh falloff.

4. **Compositor Polish (Fog Glow Glare & Lens Dispersion):**
   - Pass the rendered image through `CompositorNodeGlare` (`Fog Glow` bloom, `Threshold = 1.15`, `Strength = 0.25`) followed by `CompositorNodeLensdist` (`Dispersion = 0.008` subtle chromatic aberration at frame boundaries) and an S-curve (`CompositorNodeCurveRGB`).

5. **Volumetric Atmospheric Depth (`Global_Atmosphere_Volume_Box`):**
   - **Atmospheric Volume:** Create a large cube (`Global_Atmosphere_Volume_Box`) assigned a `ShaderNodeVolumePrincipled` material with ultra-low `Density` (`0.0015–0.003`) and high forward-scattering `Anisotropy = 0.70` so sunlight and architectural fixtures naturally scatter through the air, carving volumetric rays and authentic aerial perspective.

---

## 5. Reference Python Implementation (`bpy`)

Always include and execute this studio polish pipeline at the end of your Blender generation script:

```python
import bpy
import math


def create_overhead_gobo_canopy(location=(0.0, 0.0, 18.0), size=40.0):
    """Creates Light_Gobo_Canopy overhead plane to cast dappled foliage/slat shadows."""
    gobo_name = "Light_Gobo_Canopy"
    gobo_obj = bpy.data.objects.get(gobo_name)
    if not gobo_obj:
        bpy.ops.mesh.primitive_plane_add(size=size, location=location)
        gobo_obj = bpy.context.active_object
        gobo_obj.name = gobo_name
        gobo_obj.rotation_euler = (math.radians(15.0), math.radians(-10.0), 0.0)

    # Hide gobo plane from camera rays while keeping shadow casting active
    if hasattr(gobo_obj, "visible_camera"):
        gobo_obj.visible_camera = False

    mat_name = "Light_Gobo_Canopy_Mat"
    mat = bpy.data.materials.get(mat_name) or bpy.data.materials.new(name=mat_name)
    mat.use_nodes = True
    if hasattr(mat, "blend_method"):
        mat.blend_method = 'CLIP'

    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    out = nodes.new(type='ShaderNodeOutputMaterial')
    mix_shader = nodes.new(type='ShaderNodeMixShader')
    transparent = nodes.new(type='ShaderNodeBsdfTransparent')
    diffuse = nodes.new(type='ShaderNodeBsdfDiffuse')
    diffuse.inputs['Color'].default_value = (0.02, 0.02, 0.02, 1.0)

    voronoi = nodes.new(type='ShaderNodeTexVoronoi')
    voronoi.inputs['Scale'].default_value = 8.0

    noise = nodes.new(type='ShaderNodeTexNoise')
    noise.inputs['Scale'].default_value = 4.5
    noise.inputs['Detail'].default_value = 6.0

    mix_mask = nodes.new(type='ShaderNodeMix')
    mix_mask.data_type = 'FLOAT'
    mix_mask.inputs['Factor'].default_value = 0.5

    ramp = nodes.new(type='ShaderNodeValToRGB')
    ramp.color_ramp.interpolation = 'CONSTANT'
    ramp.color_ramp.elements[0].position = 0.48
    ramp.color_ramp.elements[1].position = 0.52

    links.new(voronoi.outputs['Distance'], mix_mask.inputs['A'])
    links.new(noise.outputs['Fac'], mix_mask.inputs['B'])
    links.new(mix_mask.outputs['Result'], ramp.inputs['Fac'])
    links.new(ramp.outputs['Color'], mix_shader.inputs['Fac'])
    links.new(transparent.outputs['BSDF'], mix_shader.inputs[1])
    links.new(diffuse.outputs['BSDF'], mix_shader.inputs[2])
    links.new(mix_shader.outputs['Shader'], out.inputs['Surface'])

    if gobo_obj.data.materials:
        gobo_obj.data.materials[0] = mat
    else:
        gobo_obj.data.materials.append(mat)


def stack_material_imperfections(mat):
    """Stacks Micro-Roughness (LINEAR_LIGHT) and Pointiness/AO Edge-Wear & Crevice Dirt on a Principled BSDF."""
    if not mat or not mat.use_nodes:
        return
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links

    bsdf = next((n for n in nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if not bsdf or mat.get("studio_imperfections_applied"):
        return

    # --- A. High-Frequency Micro-Roughness Stacking (LINEAR_LIGHT, Factor = 0.20) ---
    micro_noise = nodes.new(type='ShaderNodeTexNoise')
    micro_noise.inputs['Scale'].default_value = 45.0
    micro_noise.inputs['Detail'].default_value = 8.0
    micro_noise.inputs['Roughness'].default_value = 0.65

    rough_mix = nodes.new(type='ShaderNodeMix')
    rough_mix.data_type = 'RGBA'
    rough_mix.blend_type = 'LINEAR_LIGHT'
    rough_mix.inputs['Factor'].default_value = 0.20

    rough_input = bsdf.inputs['Roughness']
    if rough_input.is_linked:
        orig_rough_socket = rough_input.links[0].from_socket
        links.new(orig_rough_socket, rough_mix.inputs['A'])
    else:
        val = rough_input.default_value
        rough_mix.inputs['A'].default_value = (val, val, val, 1.0)

    links.new(micro_noise.outputs['Fac'], rough_mix.inputs['B'])

    # --- B. Geometry Pointiness (Edge-Wear) & Ambient Occlusion (Crevice Dirt) ---
    geom = nodes.new(type='ShaderNodeNewGeometry')
    pointiness_ramp = nodes.new(type='ShaderNodeValToRGB')
    pointiness_ramp.color_ramp.elements[0].position = 0.49
    pointiness_ramp.color_ramp.elements[1].position = 0.56
    links.new(geom.outputs['Pointiness'], pointiness_ramp.inputs['Fac'])

    ao_node = nodes.new(type='ShaderNodeAmbientOcclusion')
    ao_node.inputs['Distance'].default_value = 0.25
    ao_ramp = nodes.new(type='ShaderNodeValToRGB')
    ao_ramp.color_ramp.elements[0].position = 0.20
    ao_ramp.color_ramp.elements[1].position = 0.75
    links.new(ao_node.outputs['AO'], ao_ramp.inputs['Fac'])

    # Combine Pointiness edge-wear and AO crevice dirt into Base Color
    base_input = bsdf.inputs['Base Color']
    edge_mix = nodes.new(type='ShaderNodeMix')
    edge_mix.data_type = 'RGBA'
    edge_mix.blend_type = 'MIX'
    edge_mix.inputs['B'].default_value = (0.78, 0.74, 0.68, 1.0)  # Subtle worn edge tint

    ao_color_mix = nodes.new(type='ShaderNodeMix')
    ao_color_mix.data_type = 'RGBA'
    ao_color_mix.blend_type = 'MULTIPLY'
    ao_color_mix.inputs['Factor'].default_value = 0.35

    if base_input.is_linked:
        orig_base_socket = base_input.links[0].from_socket
        links.new(orig_base_socket, edge_mix.inputs['A'])
    else:
        edge_mix.inputs['A'].default_value = base_input.default_value

    links.new(pointiness_ramp.outputs['Color'], edge_mix.inputs['Factor'])
    links.new(edge_mix.outputs['Result'], ao_color_mix.inputs['A'])
    links.new(ao_ramp.outputs['Color'], ao_color_mix.inputs['B'])
    links.new(ao_color_mix.outputs['Result'], base_input)

    # Feed edge-wear into final Roughness
    rough_edge_mix = nodes.new(type='ShaderNodeMix')
    rough_edge_mix.data_type = 'RGBA'
    rough_edge_mix.blend_type = 'SCREEN'
    rough_edge_mix.inputs['Factor'].default_value = 0.25
    links.new(rough_mix.outputs['Result'], rough_edge_mix.inputs['A'])
    links.new(pointiness_ramp.outputs['Color'], rough_edge_mix.inputs['B'])
    links.new(rough_edge_mix.outputs['Result'], rough_input)

    mat["studio_imperfections_applied"] = True


def apply_blender_visual_strengths(primary_focus_location=(0.0, 0.0, 2.0)):
    """Executes all 5 Blender-native visual strengths: Gobos, Textured Lights, Micro-Roughness/Pointiness, 35mm DoF, Compositor, and Volumetric Atmosphere."""

    # 1. Textured Area/Spot Lights + Overhead Dappled Gobo Plane
    create_overhead_gobo_canopy()
    for light_obj in [o for o in bpy.data.objects if o.type == "LIGHT"]:
        light = light_obj.data
        if light.type in {'AREA', 'SPOT'}:
            light.use_nodes = True
            nodes = light.node_tree.nodes
            links = light.node_tree.links
            emission = next((n for n in nodes if n.type == 'EMISSION'), None)
            if emission:
                noise = nodes.new(type='ShaderNodeTexNoise')
                noise.inputs['Scale'].default_value = 6.0

                ramp = nodes.new(type='ShaderNodeValToRGB')
                ramp.color_ramp.elements[0].position = 0.65
                ramp.color_ramp.elements[1].position = 1.0

                links.new(noise.outputs['Fac'], ramp.inputs['Fac'])
                links.new(ramp.outputs['Color'], emission.inputs['Color'])

    # 2. High-Frequency Micro-Roughness & Pointiness/AO Imperfection Stacking
    for mat in bpy.data.materials:
        if mat.name not in {"Light_Gobo_Canopy_Mat", "Global_Atmosphere_Volume_Mat"}:
            stack_material_imperfections(mat)

    # 3. Physical Camera Calibration (Full-Frame 35mm Sensor & True Depth of Field)
    wide_cam_tags = ("Cam_01", "Cam_02", "Cam_03", "Cam_04", "Cam_05")
    for cam_obj in [o for o in bpy.data.objects if o.type == "CAMERA"]:
        cam = cam_obj.data
        cam.sensor_fit = 'HORIZONTAL'
        cam.sensor_width = 36.0
        cam.dof.use_dof = True

        target_name = f"Focus_Target_{cam_obj.name}"
        target_obj = bpy.data.objects.get(target_name)
        if not target_obj:
            target_obj = bpy.data.objects.new(target_name, None)
            target_obj.empty_display_type = 'CROSS'
            target_obj.location = primary_focus_location
            bpy.context.scene.collection.objects.link(target_obj)

        cam.dof.focus_object = target_obj
        if any(tag in cam_obj.name for tag in wide_cam_tags):
            cam.dof.aperture_fstop = 4.8  # 4.0–5.6 for wide architectural shots
        else:
            cam.dof.aperture_fstop = 2.2  # 1.8–2.8 for close/medium shots

    # 4. Compositor Polish (Fog Glow Glare & Lens Dispersion + S-Curve)
    scene = bpy.context.scene
    scene.use_nodes = True
    c_tree = scene.node_tree
    if c_tree:
        c_nodes = c_tree.nodes
        c_links = c_tree.links
        c_nodes.clear()

        render_layers = c_nodes.new(type='CompositorNodeRLayers')
        composite = c_nodes.new(type='CompositorNodeComposite')

        glare = c_nodes.new(type='CompositorNodeGlare')
        glare.glare_type = 'FOG_GLOW'
        glare.threshold = 1.15
        glare.mix = 0.25

        lensdist = c_nodes.new(type='CompositorNodeLensdist')
        if 'Dispersion' in lensdist.inputs:
            lensdist.inputs['Dispersion'].default_value = 0.008
        elif 'Distortion' in lensdist.inputs and len(lensdist.inputs) > 2:
            lensdist.inputs[2].default_value = 0.008

        curves = c_nodes.new(type='CompositorNodeCurveRGB')
        curve_rgb = curves.mapping.curves[3]
        curve_rgb.points.new(0.25, 0.20)
        curve_rgb.points.new(0.75, 0.80)
        curves.mapping.update()

        c_links.new(render_layers.outputs['Image'], glare.inputs['Image'])
        c_links.new(glare.outputs['Image'], lensdist.inputs['Image'])
        c_links.new(lensdist.outputs['Image'], curves.inputs['Image'])
        c_links.new(curves.outputs['Image'], composite.inputs['Image'])

    # 5. Volumetric Atmospheric Depth (Global_Atmosphere_Volume_Box)
    vol_name = "Global_Atmosphere_Volume_Box"
    if vol_name not in bpy.data.objects:
        bpy.ops.mesh.primitive_cube_add(size=120.0, location=(0.0, 0.0, 20.0))
        vol_box = bpy.context.active_object
        vol_box.name = vol_name
        vol_box.display_type = 'WIRE'

        vol_mat = bpy.data.materials.new(name="Global_Atmosphere_Volume_Mat")
        vol_mat.use_nodes = True
        v_nodes = vol_mat.node_tree.nodes
        v_links = vol_mat.node_tree.links
        v_nodes.clear()

        out_node = v_nodes.new(type='ShaderNodeOutputMaterial')
        vol_principled = v_nodes.new(type='ShaderNodeVolumePrincipled')
        vol_principled.inputs['Density'].default_value = 0.0018
        vol_principled.inputs['Anisotropy'].default_value = 0.70

        v_links.new(vol_principled.outputs['Volume'], out_node.inputs['Volume'])
        vol_box.data.materials.append(vol_mat)
```