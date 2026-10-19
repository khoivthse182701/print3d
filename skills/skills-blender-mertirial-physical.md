---
name: character_garment_physics_production
description: End-to-end 3D character rigging, procedural 2D-to-3D garment pattern sewing, automatic weight transfer, Pin Group setup, and real-time/baked cloth physics simulation in Blender using Python (bpy).
---

# 3D Character & Garment Physics Production Skill (Blender `bpy`)

Use this skill whenever the user asks to create, rig, dress, or simulate 3D characters and 3D clothing with realistic cloth physics in Blender (or prepare rigged characters with physics-ready garments for Unity/Unreal).

---

## 1. Core Architecture & Pipeline Rules (Mandatory Execution Order)

Whenever building a 3D character with simulated clothing, you MUST execute the pipeline in these exact 5 stages. Never add a `Cloth` modifier before rigging weights and collision cages are properly configured.

### Stage 1: Character Base Mesh, Armature & Collision Body Setup
1. **Metric Human Scale:** Enforce metric units (`scale_length = 1.0`). Character height must be realistic (`1.35m–1.80m`) because Blender's cloth solver calculates gravity ($9.81\text{ m/s}^2$) and air drag based on real-world meters.
2. **A-Pose / T-Pose Rest State:** Model and rig the character in a clean A-Pose or T-Pose at Frame 1 so garment panels can be positioned without initial arm-to-torso intersections.
3. **Collision Body Configuration:**
   - Apply a `COLLISION` modifier to the character's body mesh (`Body_Collision`).
   - Set `collision.thickness_outer = 0.002` to `0.004` (2mm–4mm buffer), `thickness_inner = 0.001`, and `damping = 0.5`, `cloth_friction = 5.0` to prevent cloth sliding off shoulders.

### Stage 2: 3D Garment Generation & 2D-to-3D Pattern Sewing
Choose one of two garment generation methods depending on garment complexity:
1. **Method A — Pattern Sewing (`use_sewing_springs`):**
   - Place front and back 2D subdivided pattern grids closely in front of and behind the torso/limbs.
   - Connect boundary vertices between front and back panels using loose edges (faces deleted, edges retained) to act as sewing threads.
   - Enable `cloth_mod.settings.use_sewing_springs = True`, set `sewing_force_max = 15.0`, and `shrinking_factor = -0.02` (slight expansion) or `0.02` (tight fit).
2. **Method B — Shell Extraction / Tube Lofting:**
   - Duplicate/extract torso or limb surface regions, offset outward along vertex normals (`Displace` or `Shrinkwrap` with offset `0.005m`), remesh into uniform quads (`Remesh` or even loop cuts), and relax vertices.
   - **Rule:** Cloth meshes MUST have uniform quad topology. Never run cloth physics on long, stretched triangles or un-subdivided N-gons.

### Stage 3: Automatic Skinning Weight Transfer (`DataTransferModifier`)
Before simulating physics, garments must follow the character's skeletal animation:
1. Parent the garment object to the character's `Armature` with Empty Groups (`ARMATURE_NAME`).
2. Add a `DATA_TRANSFER` modifier to the garment with `object = body_mesh`:
   - Enable `use_vert_data = True` and `data_types_verts = {'VGROUP_WEIGHTS'}`.
   - Set `vert_mapping = 'POLYINTERP_NEAREST'`, `layers_vgroup_select_src = 'ALL'`, `layers_vgroup_select_dst = 'NAME'`.
3. Execute `bpy.ops.object.datalayout_transfer(modifier=dt_mod.name)` and apply the `DATA_TRANSFER` modifier so vertex weights are permanently baked onto the garment.

### Stage 4: Pin Group Generation & Modifier Stack Ordering
1. **Automatic Pin Group (`Pin_Cloth`):**
   - Create a vertex group named `Pin_Cloth` on the garment.
   - Assign `weight = 1.0` to anchor zones (collar, waistband, shoulder seams, belt loops), `weight = 0.3–0.6` to transition zones (chest/upper hip), and `weight = 0.0` to free-flowing zones (shirt hem, sleeves, cape, skirt, loose trousers).
2. **Strict Modifier Stack Order (Top to Bottom):**
   - `01_Armature`: Deforms the garment with the character's skeleton.
   - `02_Cloth`: Simulates physics on unpinned vertices (`use_dynamic_mesh = True`).
   - `03_CorrectiveSmooth`: Smooths harsh folding artifacts (`iterations = 5`, `smooth_type = 'LENGTH_WEIGHTED'`).
   - `04_Solidify`: Adds physical fabric thickness (`thickness = 0.0025`, `offset = 1.0`) **after** simulation so the solver only calculates single-sided faces.
   - `05_Subdivision`: Final render smoothing (`levels = 1`, `render_levels = 2`).

### Stage 5: Material Fabric Presets & Simulation Warmup / Baking
1. Apply accurate physical presets to `cloth_mod.settings` based on fabric type:
   - **Cotton / Traditional Tunic (Áo vải thô / Áo chàm):** `mass = 0.3`, `tension_stiffness = 15.0`, `compression_stiffness = 15.0`, `shear_stiffness = 15.0`, `bending_stiffness = 0.5`.
   - **Silk / Light Scarf (Khăn quàng / Lụa):** `mass = 0.15`, `tension_stiffness = 5.0`, `compression_stiffness = 5.0`, `shear_stiffness = 5.0`, `bending_stiffness = 0.05`.
   - **Denim / Heavy Coat / Leather:** `mass = 0.6`, `tension_stiffness = 40.0`, `compression_stiffness = 40.0`, `shear_stiffness = 40.0`, `bending_stiffness = 5.0`.
2. **Warmup Pre-Roll (Frames 1 to 25):** Keep the character in rest pose from Frame 1 to Frame 20 so sewing springs stitch the garment and gravity settles the cloth naturally before walk/run animation starts at Frame 25.

---

## 2. Reference Python Implementation (`bpy`)

Always include and run this modular pipeline when equipping and simulating clothing on a 3D character:

```python
import bpy
import bmesh


def setup_body_collision(body_obj, outer_thickness=0.003, friction=5.0):
    """Configures the character body mesh as a high-accuracy cloth collision obstacle."""
    if not body_obj or body_obj.type != 'MESH':
        return
    
    coll_mod = next((m for m in body_obj.modifiers if m.type == 'COLLISION'), None)
    if not coll_mod:
        coll_mod = body_obj.modifiers.new(name="Body_Collision", type='COLLISION')
    
    settings = body_obj.collision
    settings.use = True
    settings.thickness_outer = outer_thickness
    settings.thickness_inner = 0.001
    settings.cloth_friction = friction
    settings.damping = 0.5
    settings.use_culling = True


def transfer_skin_weights_to_garment(body_obj, garment_obj, armature_obj):
    """Transfers skeletal vertex weights from the character body to the garment and binds Armature."""
    bpy.ops.object.select_all(action='DESELECT')
    bpy.context.view_layer.objects.active = garment_obj
    garment_obj.select_set(True)

    # 1. Ensure Armature Modifier exists at the top of the stack
    arm_mod = next((m for m in garment_obj.modifiers if m.type == 'ARMATURE'), None)
    if not arm_mod:
        arm_mod = garment_obj.modifiers.new(name="Armature", type='ARMATURE')
    arm_mod.object = armature_obj
    arm_mod.use_deform_preserve_volume = True

    # 2. Add DataTransfer Modifier to copy all Vertex Groups from Body
    dt_mod = garment_obj.modifiers.new(name="WeightTransfer", type='DATA_TRANSFER')
    dt_mod.object = body_obj
    dt_mod.use_vert_data = True
    dt_mod.data_types_verts = {'VGROUP_WEIGHTS'}
    dt_mod.vert_mapping = 'POLYINTERP_NEAREST'
    dt_mod.layers_vgroup_select_src = 'ALL'
    dt_mod.layers_vgroup_select_dst = 'NAME'

    # Generate data layers and apply modifier
    bpy.ops.object.datalayout_transfer(modifier=dt_mod.name)
    bpy.ops.object.modifier_apply(modifier=dt_mod.name)


def generate_height_based_pin_group(
    garment_obj,
    group_name="Pin_Cloth",
    pin_z_min=1.15,
    pin_z_max=1.35,
    falloff_distance=0.12
):
    """
    Automatically creates a Pin_Cloth vertex group based on world Z height
    (e.g., pinning collar/shoulders or waistband while leaving lower hem free).
    """
    vgroup = garment_obj.vertex_groups.get(group_name)
    if not vgroup:
        vgroup = garment_obj.vertex_groups.new(name=group_name)

    world_matrix = garment_obj.matrix_world
    for v in garment_obj.data.vertices:
        world_z = (world_matrix @ v.co).z
        if pin_z_min <= world_z <= pin_z_max:
            weight = 1.0
        elif pin_z_min - falloff_distance <= world_z < pin_z_min:
            # Smooth transition from pinned (1.0) to free cloth (0.0)
            weight = (world_z - (pin_z_min - falloff_distance)) / falloff_distance
        else:
            weight = 0.0

        if weight > 0.0:
            vgroup.add([v.index], max(0.0, min(1.0, weight)), 'REPLACE')
        else:
            vgroup.remove([v.index])

    return vgroup.name


def apply_cloth_physics_stack(
    garment_obj,
    pin_group_name="Pin_Cloth",
    preset="COTTON",
    use_sewing=False,
    sewing_force=15.0,
    shrink_factor=0.0,
    fabric_thickness=0.0025
):
    """
    Configures the complete studio modifier stack on a 3D garment:
    Armature -> Cloth -> CorrectiveSmooth -> Solidify -> Subdivision.
    """
    # 1. Cloth Modifier
    cloth_mod = next((m for m in garment_obj.modifiers if m.type == 'CLOTH'), None)
    if not cloth_mod:
        cloth_mod = garment_obj.modifiers.new(name="Cloth_Simulation", type='CLOTH')

    cs = cloth_mod.settings
    cc = cloth_mod.collision_settings

    # Quality & Solver
    cs.quality = 10
    cs.time_scale = 1.0
    cs.use_dynamic_mesh = True

    # Fabric Physical Presets
    presets = {
        "COTTON": (0.30, 15.0, 15.0, 15.0, 0.50),
        "SILK":   (0.15,  5.0,  5.0,  5.0, 0.05),
        "DENIM":  (0.60, 40.0, 40.0, 40.0, 5.00),
    }
    mass, tension, compression, shear, bending = presets.get(preset.upper(), presets["COTTON"])
    cs.mass = mass
    cs.tension_stiffness = tension
    cs.compression_stiffness = compression
    cs.shear_stiffness = shear
    cs.bending_stiffness = bending

    # Damping
    cs.tension_damping = 5.0
    cs.compression_damping = 5.0
    cs.shear_damping = 5.0
    cs.bending_damping = 0.5

    # Pinning & Sewing Springs
    if pin_group_name and pin_group_name in garment_obj.vertex_groups:
        cs.vertex_group_mass = pin_group_name
        cs.pin_stiffness = 1.0

    cs.use_sewing_springs = use_sewing
    if use_sewing:
        cs.sewing_force_max = sewing_force
    cs.shrinking_factor = shrink_factor

    # Collisions & Self-Collisions
    cc.use_collision = True
    cc.collision_quality = 6
    cc.distance_min = 0.002
    cc.use_self_collision = True
    cc.self_friction = 5.0
    cc.self_distance_min = 0.002

    # 2. Corrective Smooth (prevents jagged cloth spikes)
    smooth_mod = garment_obj.modifiers.get("Cloth_CorrectiveSmooth")
    if not smooth_mod:
        smooth_mod = garment_obj.modifiers.new(name="Cloth_CorrectiveSmooth", type='CORRECTIVE_SMOOTH')
    smooth_mod.iterations = 6
    smooth_mod.smooth_type = 'LENGTH_WEIGHTED'
    smooth_mod.use_only_smooth = True

    # 3. Solidify (adds real-world fabric thickness after physics)
    solid_mod = garment_obj.modifiers.get("Cloth_Solidify")
    if not solid_mod:
        solid_mod = garment_obj.modifiers.new(name="Cloth_Solidify", type='SOLIDIFY')
    solid_mod.thickness = fabric_thickness
    solid_mod.offset = 1.0
    solid_mod.use_rim = True

    # 4. Subdivision Surface
    subd_mod = garment_obj.modifiers.get("Cloth_Subsurf")
    if not subd_mod:
        subd_mod = garment_obj.modifiers.new(name="Cloth_Subsurf", type='SUBSURF')
    subd_mod.levels = 1
    subd_mod.render_levels = 2


def equip_and_setup_character_garment(
    body_name,
    armature_name,
    garment_name,
    pin_z_min=1.15,
    pin_z_max=1.35,
    preset="COTTON",
    use_sewing=False,
    bake_frames=(1, 120)
):
    """Master pipeline function to bind, weight-transfer, pin, and simulate a 3D garment on a character."""
    body_obj = bpy.data.objects.get(body_name)
    arm_obj = bpy.data.objects.get(armature_name)
    garment_obj = bpy.data.objects.get(garment_name)

    if not all([body_obj, arm_obj, garment_obj]):
        raise ValueError("Missing Body, Armature, or Garment object in bpy.data.objects.")

    # Step 1: Collision on Body
    setup_body_collision(body_obj)

    # Step 2: Transfer Skeletal Weights from Body to Garment
    transfer_skin_weights_to_garment(body_obj, garment_obj, arm_obj)

    # Step 3: Create Pin Group for Collar/Waist Anchor
    pin_group = generate_height_based_pin_group(
        garment_obj,
        group_name="Pin_Cloth",
        pin_z_min=pin_z_min,
        pin_z_max=pin_z_max
    )

    # Step 4: Apply Cloth Physics & Post-Sim Modifiers
    apply_cloth_physics_stack(
        garment_obj,
        pin_group_name=pin_group,
        preset=preset,
        use_sewing=use_sewing
    )

    # Step 5: Configure Point Cache Frame Range
    cloth_mod = garment_obj.modifiers.get("Cloth_Simulation")
    if cloth_mod and bake_frames:
        cloth_mod.point_cache.frame_start = bake_frames[0]
        cloth_mod.point_cache.frame_end = bake_frames[1]
```

---

## 3. Game Engine Export Guidelines (Unity / Unreal Integration)

If the user requests exporting the character and clothing to **Unity** or **Unreal Engine**:
1. **Do NOT rely on Blender's Cloth Modifier inside FBX:** FBX does not export Blender's internal cloth solver to Unity/Unreal.
2. **Workflow A — Unity Cloth Component / Chaos Cloth:**
   - Apply `DataTransfer` skeletal weights so the garment moves with the rig.
   - Do **not** apply `Solidify` if using Unity's built-in `Cloth` component (Unity Cloth works best on single-sided meshes to avoid double-layer self-penetration).
   - Export Vertex Colors (`Pin_Color`) where Red = `0.0` (pinned) and Red = `1.0` (max cloth movement distance) to speed up constraint painting in the engine.
3. **Workflow B — Secondary Physics Bones (SpringBones / Dynamic Bones / Cloth Bones):**
   - For lower hems, scarves, or bags that need lightweight real-time physics in Unity C#, generate chains of 3–4 secondary bones (`Bone_Skirt_F_01..03`, `Bone_Scarf_01..04`) parented to the Spine/Pelvis, bind the garment to those bones, and let the engine's C# spring-bone physics drive them.