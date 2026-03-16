"""PSD file parser using psd-tools library (Python-side, no Unity needed)."""
import hashlib
import io
import os
import re
import json
from typing import Optional


def parse_psd(psd_path: str, image_output_dir: str = None,
              unity_project_path: str = None, export_images: bool = True) -> dict:
    """Parse a PSD/PSB file, optionally export layer images, and return the layer tree.

    Args:
        psd_path: Absolute path to the PSD/PSB file.
        image_output_dir: Unity Assets-relative path for exported images
                          (e.g. "Assets/UI/MyPSD"). Required when export_images=True.
        unity_project_path: Unity project root (contains Assets/).
                            If None, image_output_dir is treated as an absolute path.
        export_images: If True (default), export image layers as PNG files.
                       If False, only parse structure without exporting images.

    Returns:
        Dict with canvasWidth, canvasHeight, psdName, and layers tree.
    """
    try:
        from psd_tools import PSDImage
    except ImportError:
        return {"error": "psd-tools is not installed. Run: pip install psd-tools"}

    psd_path = os.path.realpath(psd_path)
    if not os.path.isfile(psd_path):
        return {"error": f"File not found: {psd_path}"}

    ext = os.path.splitext(psd_path)[1].lower()
    if ext not in (".psd", ".psb"):
        return {"error": f"Not a valid PSD/PSB file: {psd_path}"}

    # Resolve absolute output directory
    abs_output_dir = None
    if export_images:
        if not image_output_dir:
            return {"error": "image_output_dir is required when export_images=True"}
        if unity_project_path:
            abs_output_dir = os.path.join(unity_project_path, image_output_dir)
        else:
            abs_output_dir = image_output_dir
        os.makedirs(abs_output_dir, exist_ok=True)

    psd = PSDImage.open(psd_path)
    psd_name = os.path.splitext(os.path.basename(psd_path))[0]

    layers = []
    exported_images = []
    _layer_counter = [0]
    _image_hash_map = {}  # md5 -> (filename, assetPath) for deduplication
    _parse_layers(psd, layers, abs_output_dir, image_output_dir or "",
                  _layer_counter, exported_images, _image_hash_map, export_images)

    result = {
        "canvasWidth": psd.width,
        "canvasHeight": psd.height,
        "psdName": psd_name,
        "layerCount": _layer_counter[0],
        "layers": layers,
    }
    if export_images:
        result["exportedImages"] = exported_images

    return result


def get_psd_summary(psd_path: str) -> dict:
    """Get a quick summary of a PSD/PSB file without exporting anything.

    Returns canvas size, layer counts by type, and a compact layer tree.
    """
    try:
        from psd_tools import PSDImage
    except ImportError:
        return {"error": "psd-tools is not installed. Run: pip install psd-tools"}

    psd_path = os.path.realpath(psd_path)
    if not os.path.isfile(psd_path):
        return {"error": f"File not found: {psd_path}"}

    ext = os.path.splitext(psd_path)[1].lower()
    if ext not in (".psd", ".psb"):
        return {"error": f"Not a valid PSD/PSB file: {psd_path}"}

    psd = PSDImage.open(psd_path)
    psd_name = os.path.splitext(os.path.basename(psd_path))[0]

    counts = {"group": 0, "text": 0, "image": 0, "fillcolor": 0}
    tree = _build_summary_tree(psd, counts)

    return {
        "psdName": psd_name,
        "canvasWidth": psd.width,
        "canvasHeight": psd.height,
        "layerCounts": counts,
        "totalLayers": sum(counts.values()),
        "layerTree": tree,
    }


def get_psd_layer_detail(psd_path: str, layer_path: str) -> dict:
    """Get detailed info about a specific layer by path (e.g. 'group1/layerName').

    Returns full layer properties including text content, colors, blend mode, etc.
    """
    try:
        from psd_tools import PSDImage
    except ImportError:
        return {"error": "psd-tools is not installed. Run: pip install psd-tools"}

    psd_path = os.path.realpath(psd_path)
    if not os.path.isfile(psd_path):
        return {"error": f"File not found: {psd_path}"}

    psd = PSDImage.open(psd_path)

    # Navigate to the target layer by path
    parts = [p.strip() for p in layer_path.split('/') if p.strip()]
    layer = _find_layer_by_path(psd, parts)
    if layer is None:
        return {"error": f"Layer not found: {layer_path}"}

    return _extract_layer_detail(layer)


def _find_layer_by_path(group, path_parts):
    """Find a layer by navigating a path like ['group1', 'layer2']."""
    if not path_parts:
        return None
    target = path_parts[0]
    remaining = path_parts[1:]
    for layer in group:
        if layer.name == target:
            if not remaining:
                return layer
            if layer.is_group():
                return _find_layer_by_path(layer, remaining)
            return None
    return None


def _extract_layer_detail(layer) -> dict:
    """Extract comprehensive detail from a single layer."""
    ui_type = _detect_ui_type(layer.name)
    display_name = layer.name
    if ui_type and '.' in display_name:
        display_name = display_name.rsplit('.', 1)[0]

    detail = {
        "name": display_name,
        "originalName": layer.name,
        "kind": layer.kind,
        "visible": layer.is_visible(),
        "left": layer.left,
        "top": layer.top,
        "right": getattr(layer, 'right', layer.left + layer.width),
        "bottom": getattr(layer, 'bottom', layer.top + layer.height),
        "width": max(layer.width, 0),
        "height": max(layer.height, 0),
        "opacity": layer.opacity,
        "blendMode": str(getattr(layer, 'blend_mode', 'normal')),
    }

    if ui_type:
        detail["uiType"] = ui_type

    # Extract layer effects
    layer_effects = _extract_layer_effects(layer)
    if layer_effects:
        detail["effects"] = layer_effects

    inner_text = _get_inner_text_layer(layer)

    if layer.is_group():
        detail["type"] = "group"
        detail["childCount"] = len(list(layer))
        detail["children"] = [
            {"name": c.name, "kind": c.kind, "visible": c.is_visible()}
            for c in layer
        ]
    elif inner_text is not None:
        detail["type"] = "text"
        detail["textProperties"] = _extract_text_properties(inner_text)
        if layer.kind == 'smartobject':
            detail["isSmartObjectText"] = True
    elif layer.kind in ('solidcolorfill', 'gradientfill', 'patternfill'):
        detail["type"] = "fillcolor"
        detail["fillColor"] = _extract_fill_color(layer)
    else:
        detail["type"] = "image"
        if layer.kind == 'smartobject':
            detail["isSmartObject"] = True

    return detail


def _build_summary_tree(group, counts) -> list:
    """Build a compact layer tree for summary (name + type only)."""
    tree = []
    for layer in list(group):
        if not layer.is_visible():
            continue

        ui_type = _detect_ui_type(layer.name)
        display_name = layer.name
        if ui_type and '.' in display_name:
            display_name = display_name.rsplit('.', 1)[0]

        inner_text = _get_inner_text_layer(layer)

        if layer.is_group():
            counts["group"] += 1
            children = _build_summary_tree(layer, counts)
            node = {"name": display_name, "type": "group", "children": children}
        elif inner_text is not None:
            counts["text"] += 1
            text_content = ""
            try:
                text_content = inner_text.text or ""
            except Exception:
                pass
            node = {"name": display_name, "type": "text", "text": text_content[:50]}
        elif layer.kind in ('solidcolorfill', 'gradientfill', 'patternfill'):
            counts["fillcolor"] += 1
            node = {"name": display_name, "type": "fillcolor"}
        else:
            counts["image"] += 1
            node = {"name": display_name, "type": "image"}

        if ui_type:
            node["uiType"] = ui_type
        tree.append(node)
    return tree


# ---------------------------------------------------------------------------
# Internal helpers
# ---------------------------------------------------------------------------

def _safe_filename(name: str, layer_id: int) -> str:
    """Create a filesystem-safe filename from a layer name."""
    safe = re.sub(r'[<>:"/\\|?*\x00-\x1f]', '_', name)
    safe = safe.strip('. ')
    if not safe:
        safe = "layer"
    return f"{safe}_{layer_id}"


def _get_inner_text_layer(layer):
    """If this layer is (or wraps) a text layer, return the actual text layer.

    Handles:
    1. Direct TypeLayer (kind=='type')
    2. SmartObjectLayer wrapping a single TypeLayer (like psd2ui's
       GetSmartObjectInnerTextLayer)

    Returns the text layer object, or None.
    """
    if layer.kind == 'type':
        return layer
    # SmartObject wrapping a text layer
    if layer.kind == 'smartobject':
        try:
            from psd_tools import PSDImage
            so = layer.smart_object
            if so and so.data:
                inner_psd = PSDImage.open(io.BytesIO(so.data))
                inner_layers = list(inner_psd)
                if len(inner_layers) == 1 and inner_layers[0].kind == 'type':
                    return inner_layers[0]
        except Exception:
            pass
    return None


def _detect_ui_type(layer_name: str) -> Optional[str]:
    """Detect UI type marker from layer name.

    psd2ui convention: "[name].[UIType]" e.g., "btn.Button", "bg.Image"
    Returns the UIType string or None.
    """
    if '.' in layer_name:
        parts = layer_name.rsplit('.', 1)
        if len(parts) == 2 and parts[1]:
            ui_type = parts[1].strip()
            known_types = {
                'Image', 'RawImage', 'Text', 'Button', 'Dropdown',
                'InputField', 'Toggle', 'Slider', 'ScrollView', 'Mask',
                'FillColor', 'TMPText', 'TMPButton', 'TMPDropdown',
                'TMPInputField', 'TMPToggle',
                'Background', 'Button_Highlight', 'Button_Press',
                'Button_Select', 'Button_Disable', 'Button_Text',
            }
            if ui_type in known_types:
                return ui_type
    return None


def _extract_fill_color(layer) -> dict:
    """Extract fill color from a solid color fill layer."""
    color = {"r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0}
    try:
        # psd-tools: layer.data contains fill settings
        if hasattr(layer, 'data') and layer.data is not None:
            fill_data = layer.data
            # SolidColorFill has Clr key
            if hasattr(fill_data, 'get'):
                clr = fill_data.get(b'Clr ')
                if clr:
                    color["r"] = round(clr.get(b'Rd  ', 255) / 255.0, 3)
                    color["g"] = round(clr.get(b'Grn ', 255) / 255.0, 3)
                    color["b"] = round(clr.get(b'Bl  ', 255) / 255.0, 3)
        # Apply layer opacity
        color["a"] = round(layer.opacity / 255.0, 3)
    except Exception:
        pass
    return color


def _extract_text_properties(layer) -> dict:
    """Extract text properties from a text layer."""
    props = {
        "content": "",
        "fontSize": 16,
        "fontName": "",
        "color": {"r": 1.0, "g": 1.0, "b": 1.0, "a": 1.0},
        "alignment": "left",
        "bold": False,
        "italic": False,
        "lineSpacing": 1.0,
        "characterSpacing": 0.0,
    }

    try:
        props["content"] = layer.text or ""
    except Exception:
        pass

    try:
        engine_data = layer.engine_dict
        if engine_data:
            # Extract from EngineData
            style_run = engine_data.get('StyleRun', {})
            run_array = style_run.get('RunArray', [])
            if run_array:
                style_sheet = run_array[0].get('StyleSheet', {})
                style_data = style_sheet.get('StyleSheetData', {})

                font_size = style_data.get('FontSize')
                if font_size is not None:
                    props["fontSize"] = round(float(font_size))

                fill_color = style_data.get('FillColor', {})
                color_values = fill_color.get('Values', [])
                if len(color_values) >= 4:
                    props["color"] = {
                        "r": round(float(color_values[1]), 3),
                        "g": round(float(color_values[2]), 3),
                        "b": round(float(color_values[3]), 3),
                        "a": round(float(color_values[0]), 3),
                    }

                font_index = style_data.get('Font')
                if font_index is not None:
                    try:
                        font_set = layer.resource_dict.get('FontSet', [])
                        idx = int(font_index)
                        if 0 <= idx < len(font_set):
                            props["fontName"] = str(font_set[idx].get('Name', '')).strip("'")
                        else:
                            props["fontName"] = str(font_index)
                    except (ValueError, IndexError, TypeError, AttributeError):
                        props["fontName"] = str(font_index)

                # Bold / Italic
                face_name = style_data.get('FauxBold')
                if face_name:
                    props["bold"] = True
                face_italic = style_data.get('FauxItalic')
                if face_italic:
                    props["italic"] = True

                # Stroke color from character panel
                stroke_color = style_data.get('StrokeColor', {})
                stroke_values = stroke_color.get('Values', [])
                if len(stroke_values) >= 4:
                    props["strokeColor"] = {
                        "r": round(float(stroke_values[1]), 3),
                        "g": round(float(stroke_values[2]), 3),
                        "b": round(float(stroke_values[3]), 3),
                        "a": round(float(stroke_values[0]), 3),
                    }

                # Underline / Strikethrough
                if style_data.get('Underline'):
                    props["underline"] = True
                if style_data.get('Strikethrough'):
                    props["strikethrough"] = True

                # Character spacing (tracking)
                tracking = style_data.get('Tracking')
                if tracking is not None:
                    props["characterSpacing"] = round(float(tracking) / 1000.0, 3)

                # Auto leading / line height
                auto_leading = style_data.get('AutoLeading')
                leading = style_data.get('Leading')
                if leading is not None and font_size is not None and float(font_size) > 0:
                    props["lineSpacing"] = round(float(leading) / float(font_size), 3)
                elif auto_leading is not None:
                    props["lineSpacing"] = round(float(auto_leading), 3)

            # Paragraph alignment
            para_run = engine_data.get('ParagraphRun', {})
            para_array = para_run.get('RunArray', [])
            if para_array:
                para_sheet = para_array[0].get('ParagraphSheet', {})
                para_data = para_sheet.get('Properties', {})
                justification = para_data.get('Justification')
                if justification is not None:
                    align_map = {0: 'left', 1: 'right', 2: 'center'}
                    props["alignment"] = align_map.get(int(justification), 'left')
    except Exception:
        pass

    return props


def _extract_layer_effects(layer) -> list:
    """Extract enabled layer effects (stroke, shadow, color/gradient overlay)."""
    effects_list = []
    try:
        effects = layer.effects
        if not effects:
            return effects_list

        for e in effects:
            if not e.enabled:
                continue

            if e.name == 'Stroke':
                fx = {
                    "type": "stroke",
                    "size": round(float(e.size), 1),
                    "position": {b'OutF': 'outside', b'InsF': 'inside', b'CtrF': 'center'}.get(e.position, 'outside'),
                    "opacity": round(float(e.opacity), 1),
                    "blendMode": _decode_blend_mode(e.blend_mode),
                    "fillType": {b'SClr': 'color', b'GrFl': 'gradient', b'Ptrn': 'pattern'}.get(e.fill_type, 'color'),
                }
                if e.fill_type == b'SClr' and e.color:
                    fx["color"] = _parse_effect_color(e.color)
                elif e.fill_type == b'GrFl' and e.gradient:
                    fx["gradient"] = _parse_gradient(e.gradient)
                effects_list.append(fx)

            elif e.name == 'DropShadow':
                fx = {
                    "type": "dropShadow",
                    "color": _parse_effect_color(e.color) if e.color else {"r": 0, "g": 0, "b": 0, "a": 1},
                    "opacity": round(float(e.opacity), 1),
                    "angle": round(float(e.angle), 1),
                    "distance": round(float(e.distance), 1),
                    "size": round(float(e.size), 1),
                    "choke": round(float(e.choke), 1),
                }
                effects_list.append(fx)

            elif e.name == 'ColorOverlay':
                fx = {
                    "type": "colorOverlay",
                    "color": _parse_effect_color(e.color) if e.color else {"r": 1, "g": 1, "b": 1, "a": 1},
                    "opacity": round(float(e.opacity), 1),
                    "blendMode": _decode_blend_mode(e.blend_mode),
                }
                effects_list.append(fx)

            elif e.name == 'GradientOverlay':
                fx = {
                    "type": "gradientOverlay",
                    "opacity": round(float(e.opacity), 1),
                    "angle": round(float(e.angle), 1),
                    "scale": round(float(e.scale), 1),
                    "reversed": bool(e.reversed),
                    "blendMode": _decode_blend_mode(e.blend_mode),
                    "gradientType": {b'Lnr ': 'linear', b'Rdl ': 'radial'}.get(e.type, 'linear'),
                }
                if e.gradient:
                    fx["gradient"] = _parse_gradient(e.gradient)
                effects_list.append(fx)

    except Exception:
        pass
    return effects_list


def _parse_effect_color(color_dict) -> dict:
    """Parse PSD effect color {b'Rd  ': float, b'Grn ': float, b'Bl  ': float} to normalized dict."""
    return {
        "r": round(color_dict.get(b'Rd  ', 0) / 255.0, 3),
        "g": round(color_dict.get(b'Grn ', 0) / 255.0, 3),
        "b": round(color_dict.get(b'Bl  ', 0) / 255.0, 3),
        "a": 1.0,
    }


def _parse_gradient(gradient) -> dict:
    """Parse PSD gradient descriptor to a serializable dict."""
    result = {"colorStops": [], "transparencyStops": []}
    try:
        color_stops = gradient.get(b'Clrs', [])
        for cs in color_stops:
            clr = cs.get(b'Clr ', {})
            stop = {
                "location": round(cs.get(b'Lctn', 0) / 4096.0, 3),
                "midpoint": round(cs.get(b'Mdpn', 50) / 100.0, 3),
                "color": {
                    "r": round(clr.get(b'Rd  ', 0) / 255.0, 3),
                    "g": round(clr.get(b'Grn ', 0) / 255.0, 3),
                    "b": round(clr.get(b'Bl  ', 0) / 255.0, 3),
                },
            }
            result["colorStops"].append(stop)

        trans_stops = gradient.get(b'Trns', [])
        for ts in trans_stops:
            stop = {
                "location": round(ts.get(b'Lctn', 0) / 4096.0, 3),
                "midpoint": round(ts.get(b'Mdpn', 50) / 100.0, 3),
                "opacity": round(ts.get(b'Opct', 100) / 100.0, 3),
            }
            result["transparencyStops"].append(stop)
    except Exception:
        pass
    return result


def _decode_blend_mode(mode) -> str:
    """Decode PSD blend mode bytes to readable string."""
    mode_map = {
        b'Nrml': 'normal', b'Mltp': 'multiply', b'Scrn': 'screen',
        b'Ovrl': 'overlay', b'Dslv': 'dissolve', b'Drkn': 'darken',
        b'Lghn': 'lighten', b'HrdL': 'hardLight', b'SftL': 'softLight',
    }
    return mode_map.get(mode, 'normal')


def _parse_layers(group, result_list, abs_output_dir, rel_output_dir,
                  counter, exported_images, image_hash_map, export_images=True):
    """Recursively parse PSD layers into a structured list."""
    for layer in list(group):
        if not layer.is_visible():
            continue

        counter[0] += 1
        layer_id = getattr(layer, 'layer_id', counter[0])
        ui_type = _detect_ui_type(layer.name)

        # Clean display name (remove UI type suffix)
        display_name = layer.name
        if ui_type and '.' in display_name:
            display_name = display_name.rsplit('.', 1)[0]

        layer_info = {
            "name": display_name,
            "originalName": layer.name,
            "left": layer.left,
            "top": layer.top,
            "width": max(layer.width, 0),
            "height": max(layer.height, 0),
            "opacity": layer.opacity,
        }

        if ui_type:
            layer_info["uiType"] = ui_type

        # Extract layer effects (stroke, shadow, overlays)
        layer_effects = _extract_layer_effects(layer)
        if layer_effects:
            layer_info["effects"] = layer_effects

        # Check for text layer (including smart objects wrapping text)
        inner_text = _get_inner_text_layer(layer)

        if layer.is_group():
            layer_info["type"] = "group"
            layer_info["children"] = []
            _parse_layers(layer, layer_info["children"],
                          abs_output_dir, rel_output_dir, counter,
                          exported_images, image_hash_map, export_images)
        elif inner_text is not None:
            # Text layer (direct or smart-object-wrapped)
            layer_info["type"] = "text"
            layer_info["textProperties"] = _extract_text_properties(inner_text)
            if not ui_type:
                layer_info["uiType"] = "Text"
        elif layer.kind in ('solidcolorfill', 'gradientfill', 'patternfill'):
            # Fill layer - extract color, no image export needed
            layer_info["type"] = "fillcolor"
            if not ui_type:
                layer_info["uiType"] = "FillColor"
            layer_info["fillColor"] = _extract_fill_color(layer)
        else:
            # Image layer
            layer_info["type"] = "image"
            if not ui_type:
                layer_info["uiType"] = "Image"

            if export_images and abs_output_dir:
                try:
                    img = layer.composite()
                    if img and img.width > 0 and img.height > 0:
                        # Encode to PNG bytes in memory for hash-based dedup
                        buf = io.BytesIO()
                        img.save(buf, format="PNG")
                        png_bytes = buf.getvalue()
                        md5 = hashlib.md5(png_bytes).hexdigest()

                        if md5 in image_hash_map:
                            # Reuse existing identical image
                            existing = image_hash_map[md5]
                            layer_info["imagePath"] = existing["assetPath"]
                            layer_info["dedup"] = True
                        else:
                            safe_name = _safe_filename(display_name, layer_id)
                            filename = f"{safe_name}.png"
                            with open(os.path.join(abs_output_dir, filename), "wb") as f:
                                f.write(png_bytes)
                            asset_path = f"{rel_output_dir}/{filename}"
                            layer_info["imagePath"] = asset_path
                            image_hash_map[md5] = {
                                "fileName": filename,
                                "assetPath": asset_path,
                            }
                            exported_images.append({
                                "fileName": filename,
                                "assetPath": asset_path,
                                "originalLayerName": display_name,
                            })
                    else:
                        layer_info["imagePath"] = None
                        layer_info["exportError"] = "Empty layer image"
                except Exception as e:
                    layer_info["imagePath"] = None
                    layer_info["exportError"] = str(e)

        result_list.append(layer_info)
