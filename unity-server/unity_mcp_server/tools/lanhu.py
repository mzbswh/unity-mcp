"""Lanhu design platform API client for fetching designs and slices."""
import json
import os
from typing import List, Optional, Union
from urllib.parse import urlparse

import httpx

from ..config import get_lanhu_cookie, LANHU_BASE_URL, LANHU_HTTP_TIMEOUT


class NoCookieError(Exception):
    """Raised when no Lanhu cookie is configured."""
    pass


class LanhuClient:
    """Async HTTP client for Lanhu API."""

    def __init__(self, cookie: str = None):
        """Create a Lanhu client.

        Args:
            cookie: Lanhu session cookie. If None, reads from env/config file.
        """
        self._cookie = cookie or get_lanhu_cookie()
        if not self._cookie:
            raise NoCookieError()
        headers = {
            "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36",
            "Referer": "https://lanhuapp.com/web/",
            "Accept": "application/json, text/plain, */*",
            "Cookie": self._cookie,
            "sec-ch-ua": '"Chromium";v="142", "Google Chrome";v="142", "Not_A Brand";v="99"',
            "sec-ch-ua-mobile": "?0",
            "sec-ch-ua-platform": '"macOS"',
            "request-from": "web",
            "real-path": "/item/project/product",
        }
        self.client = httpx.AsyncClient(
            timeout=LANHU_HTTP_TIMEOUT, headers=headers, follow_redirects=True
        )

    async def close(self):
        await self.client.aclose()

    async def verify_cookie(self, test_url: str = None) -> dict:
        """Verify the cookie works by optionally testing with a real project URL.

        Args:
            test_url: A Lanhu project URL to test against. If provided,
                      tries to fetch designs to confirm cookie works.
                      If None, just checks cookie format.

        Returns:
            dict with 'valid' bool and 'message' str.
        """
        if not self._cookie or len(self._cookie) < 20:
            return {"valid": False, "message": "Cookie is too short or empty."}

        if "user_token=" not in self._cookie and "session=" not in self._cookie:
            return {"valid": False, "message": "Cookie missing user_token or session. Check cookie format."}

        if not test_url:
            return {"valid": True, "message": "Cookie format looks valid. Saved. It will be verified on first actual API call."}

        # If test_url provided, do a real API call to confirm
        try:
            result = await self.get_designs(test_url)
            if "error" in result:
                return {"valid": False, "message": f"Cookie test failed: {result['error']}"}
            return {"valid": True, "message": f"Cookie verified. Project: {result.get('project_name')}, {result.get('total_designs')} designs found."}
        except Exception as e:
            return {"valid": False, "message": f"Cookie test failed: {str(e)}"}

    # ------------------------------------------------------------------
    # URL parsing
    # ------------------------------------------------------------------

    @staticmethod
    def parse_url(url: str) -> dict:
        """Parse a Lanhu URL into team_id, project_id, doc_id, version_id."""
        if url.startswith("http"):
            parsed = urlparse(url)
            fragment = parsed.fragment
            if not fragment:
                raise ValueError("Invalid Lanhu URL: missing fragment part")
            url = fragment.split("?", 1)[1] if "?" in fragment else fragment

        if url.startswith("?"):
            url = url[1:]

        params = {}
        for part in url.split("&"):
            if "=" in part:
                key, value = part.split("=", 1)
                params[key] = value

        team_id = params.get("tid")
        project_id = params.get("pid")
        doc_id = params.get("docId") or params.get("image_id")
        version_id = params.get("versionId")

        if not project_id:
            raise ValueError("URL parsing failed: missing required param pid (project_id)")
        if not team_id:
            raise ValueError("URL parsing failed: missing required param tid (team_id)")

        return {
            "team_id": team_id,
            "project_id": project_id,
            "doc_id": doc_id,
            "version_id": version_id,
        }

    # ------------------------------------------------------------------
    # API: design list
    # ------------------------------------------------------------------

    async def get_designs(self, url: str) -> dict:
        """Fetch the list of design images for a Lanhu project URL."""
        params = self.parse_url(url)

        api_url = (
            f"{LANHU_BASE_URL}/api/project/images"
            f"?project_id={params['project_id']}"
            f"&team_id={params['team_id']}"
            f"&dds_status=1&position=1&show_cb_src=1&comment=1"
        )

        response = await self.client.get(api_url)
        response.raise_for_status()
        data = response.json()

        if data.get("code") != "00000":
            return {"error": data.get("msg", "Unknown error")}

        project_data = data.get("data", {})
        images = project_data.get("images", [])

        design_list = []
        for idx, img in enumerate(images, 1):
            design_list.append({
                "index": idx,
                "id": img.get("id"),
                "name": img.get("name"),
                "width": img.get("width"),
                "height": img.get("height"),
                "url": img.get("url"),
                "has_comment": img.get("has_comment", False),
                "update_time": img.get("update_time"),
            })

        return {
            "project_name": project_data.get("name"),
            "total_designs": len(design_list),
            "designs": design_list,
        }

    # ------------------------------------------------------------------
    # API: download design images for AI visual analysis
    # ------------------------------------------------------------------

    async def download_design_images(
        self, url: str, design_names: Union[str, List[str]], output_dir: str
    ) -> dict:
        """Download design images and return paths for AI analysis.

        Args:
            url: Lanhu project URL.
            design_names: 'all', a single name/index, or a list of names/indexes.
            output_dir: Directory to save downloaded images.
        """
        designs_data = await self.get_designs(url)
        if "error" in designs_data:
            return designs_data

        designs = designs_data["designs"]
        target_designs = self._resolve_targets(designs, design_names)

        if not target_designs:
            return {
                "error": "No matching design found",
                "available_designs": [d["name"] for d in designs],
            }

        os.makedirs(output_dir, exist_ok=True)

        results = []
        for design in target_designs:
            try:
                img_url = design["url"].split("?")[0]
                response = await self.client.get(img_url)
                response.raise_for_status()

                filename = f"{design['name']}.png"
                filepath = os.path.join(output_dir, filename)
                with open(filepath, "wb") as f:
                    f.write(response.content)

                results.append({
                    "design_name": design["name"],
                    "design_id": design["id"],
                    "image_path": filepath,
                    "width": design.get("width"),
                    "height": design.get("height"),
                })
            except Exception as e:
                results.append({
                    "design_name": design["name"],
                    "error": str(e),
                })

        return {
            "project_name": designs_data["project_name"],
            "total_downloaded": len([r for r in results if "image_path" in r]),
            "total_failed": len([r for r in results if "error" in r]),
            "results": results,
        }

    # ------------------------------------------------------------------
    # API: design slices (one-step: list designs -> find -> get slices)
    # ------------------------------------------------------------------

    async def get_design_slices(
        self, url: str, design_name: str, include_metadata: bool = True
    ) -> dict:
        """Get slice/asset info from a design. Internally fetches designs list first.

        Args:
            url: Lanhu project URL.
            design_name: Exact design name to fetch slices for.
            include_metadata: Include color, opacity, shadow info.
        """
        designs_data = await self.get_designs(url)
        if "error" in designs_data:
            return designs_data

        target_design = None
        for design in designs_data["designs"]:
            if design["name"] == design_name:
                target_design = design
                break

        if not target_design:
            return {
                "error": f"Design '{design_name}' not found",
                "available_designs": [d["name"] for d in designs_data["designs"]],
            }

        params = self.parse_url(url)
        return await self._fetch_slices(
            image_id=target_design["id"],
            team_id=params["team_id"],
            project_id=params["project_id"],
            include_metadata=include_metadata,
        )

    # ------------------------------------------------------------------
    # API: batch download slices
    # ------------------------------------------------------------------

    async def download_slices(
        self, url: str, design_name: str, output_dir: str,
        name_pattern: str = "layer_path",
    ) -> dict:
        """Get slices from a design and download them all to output_dir.

        Args:
            url: Lanhu project URL.
            design_name: Exact design name.
            output_dir: Directory to save downloaded slice images.
            name_pattern: Naming strategy for files.
                'layer_path' (default): derive name from layer_path (e.g. TopBar/Icon -> topbar_icon.png)
                'original': use original layer name as-is.
        """
        slices_data = await self.get_design_slices(url, design_name, include_metadata=False)
        if "error" in slices_data:
            return slices_data

        slices = slices_data.get("slices", [])
        if not slices:
            return {
                "design_name": slices_data.get("design_name", design_name),
                "message": "No slices found in this design.",
                "total_downloaded": 0,
                "results": [],
            }

        os.makedirs(output_dir, exist_ok=True)

        results = []
        used_names = set()

        for s in slices:
            try:
                download_url = s["download_url"]
                ext = f".{s.get('format', 'png')}"

                # Generate filename
                if name_pattern == "layer_path" and s.get("layer_path"):
                    base = s["layer_path"].replace("/", "_").replace(" ", "_")
                    # Clean non-filesystem-safe chars
                    base = "".join(c if c.isalnum() or c in "_-" else "_" for c in base)
                else:
                    base = s.get("name", "slice")
                    base = "".join(c if c.isalnum() or c in "_- " else "_" for c in base)
                    base = base.replace(" ", "_")

                # Deduplicate
                filename = f"{base}{ext}"
                if filename in used_names:
                    i = 2
                    while f"{base}_{i}{ext}" in used_names:
                        i += 1
                    filename = f"{base}_{i}{ext}"
                used_names.add(filename)

                filepath = os.path.join(output_dir, filename)

                response = await self.client.get(download_url)
                response.raise_for_status()
                with open(filepath, "wb") as f:
                    f.write(response.content)

                results.append({
                    "name": s.get("name"),
                    "layer_path": s.get("layer_path"),
                    "size": s.get("size"),
                    "filename": filename,
                    "filepath": filepath,
                })
            except Exception as e:
                results.append({
                    "name": s.get("name"),
                    "layer_path": s.get("layer_path"),
                    "error": str(e),
                })

        downloaded = [r for r in results if "filepath" in r]
        failed = [r for r in results if "error" in r]

        return {
            "design_name": slices_data.get("design_name", design_name),
            "canvas_size": slices_data.get("canvas_size"),
            "output_dir": output_dir,
            "total_slices": len(slices),
            "total_downloaded": len(downloaded),
            "total_failed": len(failed),
            "results": results,
        }

    # ------------------------------------------------------------------
    # Internal: fetch slices from design detail API
    # ------------------------------------------------------------------

    async def _fetch_slices(
        self, image_id: str, team_id: str, project_id: str, include_metadata: bool = True
    ) -> dict:
        """Fetch slice and text info from a design's sketch JSON."""
        # 1. Get design detail
        url = f"{LANHU_BASE_URL}/api/project/image"
        api_params = {
            "dds_status": 1,
            "image_id": image_id,
            "team_id": team_id,
            "project_id": project_id,
        }
        response = await self.client.get(url, params=api_params)
        data = response.json()

        if data.get("code") != "00000":
            return {"error": f"Failed to get design: {data.get('msg')}"}

        result = data["result"]
        latest_version = result["versions"][0]
        json_url = latest_version["json_url"]

        # 2. Download and parse sketch JSON
        json_response = await self.client.get(json_url)
        sketch_data = json_response.json()

        # 2.1 Cache raw JSON locally
        raw_data_dir = os.path.join(os.path.expanduser("~"), ".unity-mcp", "raw_slices")
        os.makedirs(raw_data_dir, exist_ok=True)
        raw_data_filename = f"{project_id}_{image_id}.json"
        raw_data_path = os.path.join(raw_data_dir, raw_data_filename)
        with open(raw_data_path, "w", encoding="utf-8") as f:
            json.dump(sketch_data, f, ensure_ascii=False, indent=2)

        # 3. Extract slices and texts
        slices = []
        texts = []
        seen_slice_ids = set()
        seen_text_ids = set()

        def _is_visible(obj):
            if obj.get("visible") is False:
                return False
            if obj.get("opacity", 1) == 0:
                return False
            return True

        def _get_layer_size(obj):
            frame = obj.get("frame")
            if isinstance(frame, dict):
                return frame.get("width", 0), frame.get("height", 0), frame.get("left", 0), frame.get("top", 0)
            w = obj.get("width", 0)
            h = obj.get("height", 0)
            x = obj.get("left", 0)
            y = obj.get("top", 0)
            if not w and not h:
                bounds = obj.get("_orgBounds") or obj.get("bounds") or obj.get("boundsWithFX")
                if isinstance(bounds, dict):
                    x = bounds.get("left", 0)
                    y = bounds.get("top", 0)
                    w = bounds.get("right", 0) - x
                    h = bounds.get("bottom", 0) - y
            return w, h, x, y

        def find_layers(obj, parent_name="", layer_path="", parent_visible=True):
            if not obj or not isinstance(obj, dict):
                return

            current_name = obj.get("name", "")
            current_path = f"{layer_path}/{current_name}" if layer_path else current_name

            self_visible = _is_visible(obj)
            visible = parent_visible and self_visible
            if not visible:
                return

            layer_type = obj.get("type", "")
            width, height, x, y = _get_layer_size(obj)
            size_str = f"{int(width)}x{int(height)}" if width and height else "unknown"

            # === Extract slices ===
            download_url = None
            slice_format = "png"

            image_val = obj.get("image")
            if isinstance(image_val, dict) and image_val.get("imageUrl"):
                download_url = image_val.get("imageUrl")

            if not download_url:
                images_val = obj.get("images")
                if isinstance(images_val, dict) and images_val:
                    for key in ["png_xxxhd", "png_xxhd", "png_xhd", "png_hd", "png"]:
                        if images_val.get(key):
                            download_url = images_val[key]
                            break
                    if not download_url:
                        for key, val in images_val.items():
                            if val and isinstance(val, str):
                                download_url = val
                                if "svg" in key:
                                    slice_format = "svg"
                                break

            if download_url:
                slice_info = {
                    "id": obj.get("id"),
                    "name": current_name,
                    "type": layer_type or "bitmap",
                    "download_url": download_url,
                    "size": size_str,
                    "format": slice_format,
                    "position": {"x": int(x), "y": int(y)},
                    "layer_path": current_path,
                }
                if parent_name:
                    slice_info["parent_name"] = parent_name

                if include_metadata:
                    metadata = LanhuClient._extract_metadata(obj)
                    if metadata:
                        slice_info["metadata"] = metadata

                slice_id = obj.get("id")
                if slice_id not in seen_slice_ids:
                    seen_slice_ids.add(slice_id)
                    slices.append(slice_info)

            # === Extract text layers ===
            if layer_type == "textLayer":
                text_info = LanhuClient._extract_text_layer(obj, current_name, current_path, parent_name, size_str, x, y)
                if text_info:
                    if include_metadata:
                        metadata = LanhuClient._extract_metadata(obj)
                        if metadata:
                            text_info["metadata"] = metadata
                    text_id = obj.get("id")
                    if text_id not in seen_text_ids:
                        seen_text_ids.add(text_id)
                        texts.append(text_info)

            # Recurse children
            for layer in obj.get("layers", []):
                find_layers(layer, current_name, current_path, visible)

        # Start extraction from artboard or info
        if sketch_data.get("artboard") and sketch_data["artboard"].get("layers"):
            for layer in sketch_data["artboard"]["layers"]:
                find_layers(layer)
        elif sketch_data.get("info"):
            for item in sketch_data["info"]:
                find_layers(item)

        return {
            "design_id": image_id,
            "design_name": result["name"],
            "version": latest_version["version_info"],
            "canvas_size": {
                "width": result.get("width"),
                "height": result.get("height"),
            },
            "total_slices": len(slices),
            "slices": slices,
            "total_texts": len(texts),
            "texts": texts,
            "raw_data_path": raw_data_path,
        }

    # ------------------------------------------------------------------
    # Helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _extract_metadata(obj) -> dict:
        """Extract style metadata from a layer object."""
        metadata = {}
        style = obj.get("style")
        if isinstance(style, dict):
            if style.get("fills"):
                metadata["fills"] = style["fills"]
            if style.get("borders"):
                metadata["borders"] = style["borders"]
            if style.get("shadows"):
                metadata["shadows"] = style["shadows"]
        layer_effects = obj.get("layerEffects")
        if isinstance(layer_effects, dict):
            metadata["layerEffects"] = layer_effects
        if "opacity" in obj:
            metadata["opacity"] = obj["opacity"]
        if obj.get("rotation"):
            metadata["rotation"] = obj["rotation"]
        if obj.get("radius"):
            metadata["border_radius"] = obj["radius"]
        return metadata

    @staticmethod
    def _extract_text_layer(obj, name, path, parent_name, size_str, x, y) -> Optional[dict]:
        """Extract text layer info, supporting both new Sketch and old PSD formats."""
        text_val = obj.get("text")
        text_info_val = obj.get("textInfo")

        text_info = {
            "id": obj.get("id"),
            "name": name,
            "size": size_str,
            "position": {"x": int(x), "y": int(y)},
            "layer_path": path,
        }
        if parent_name:
            text_info["parent_name"] = parent_name

        # New Sketch format: text is dict
        if isinstance(text_val, dict):
            text_data = text_val
            text_info["content"] = text_data.get("value", "")

            text_style = text_data.get("style")
            text_style = text_style if isinstance(text_style, dict) else {}
            font = text_style.get("font")
            font = font if isinstance(font, dict) else {}
            color = text_style.get("color")
            color = color if isinstance(color, dict) else {}

            text_info["font"] = {
                "name": font.get("name", ""),
                "postScriptName": font.get("postScriptName", ""),
                "type": font.get("type", ""),
                "size": font.get("size", 0),
                "bold": font.get("bold", False),
                "italic": font.get("italic", False),
                "underline": font.get("underline", 0),
                "linethrough": font.get("linethrough", False),
                "align": font.get("align", ""),
                "verticalAlignment": font.get("verticalAlignment", ""),
                "letterSpacing": font.get("letterSpacing", {}),
                "lineSpacing": font.get("lineSpacing", 0),
            }
            if color:
                text_info["color"] = {
                    "r": color.get("r", 0),
                    "g": color.get("g", 0),
                    "b": color.get("b", 0),
                    "a": color.get("a", 1),
                    "value": color.get("value", ""),
                }

            # Rich text styles
            styles_list = text_data.get("styles", [])
            if isinstance(styles_list, list) and len(styles_list) > 1:
                rich_styles = []
                for s in styles_list:
                    if not isinstance(s, dict):
                        continue
                    s_font = s.get("font", {}) if isinstance(s.get("font"), dict) else {}
                    s_color = s.get("color", {}) if isinstance(s.get("color"), dict) else {}
                    rich_styles.append({
                        "content": s.get("content", ""),
                        "from": s.get("from", 0),
                        "to": s.get("to", 0),
                        "font_name": s_font.get("name", ""),
                        "font_size": s_font.get("size", 0),
                        "font_type": s_font.get("type", ""),
                        "color": s_color.get("value", ""),
                    })
                if rich_styles:
                    text_info["rich_styles"] = rich_styles

            return text_info

        # Old PSD format: text is bool, data in textInfo
        if isinstance(text_info_val, dict):
            ti = text_info_val
            text_info["content"] = ti.get("text", "")
            color_raw = ti.get("color")
            color_dict = color_raw if isinstance(color_raw, dict) else {}

            text_info["font"] = {
                "name": ti.get("fontName", ""),
                "postScriptName": ti.get("fontPostScriptName", ""),
                "type": ti.get("fontStyleName", ""),
                "size": ti.get("size", 0),
                "bold": ti.get("bold", False),
                "italic": ti.get("italic", False),
                "underline": 0,
                "linethrough": False,
                "align": ti.get("justification", ""),
                "verticalAlignment": "",
                "letterSpacing": ti.get("tracking", 0),
                "lineSpacing": ti.get("leading", 0),
            }
            if color_dict:
                text_info["color"] = {
                    "r": color_dict.get("r", color_dict.get("red", 0)),
                    "g": color_dict.get("g", color_dict.get("green", 0)),
                    "b": color_dict.get("b", color_dict.get("blue", 0)),
                    "a": color_dict.get("a", 1),
                    "value": "",
                }

            # Old PSD rich text: textStyleRange
            style_range = ti.get("textStyleRange", [])
            if isinstance(style_range, list) and len(style_range) > 1:
                rich_styles = []
                full_text = ti.get("text", "")
                for s in style_range:
                    if not isinstance(s, dict):
                        continue
                    ts = s.get("textStyle", {}) if isinstance(s.get("textStyle"), dict) else {}
                    s_color = ts.get("color", {}) if isinstance(ts.get("color"), dict) else {}
                    fr = s.get("from", 0)
                    to = s.get("to", 0)
                    rich_styles.append({
                        "content": full_text[fr:to] if full_text else "",
                        "from": fr,
                        "to": to,
                        "font_name": ts.get("fontName", ""),
                        "font_size": ts.get("impliedFontSize", {}).get("value", 0) if isinstance(ts.get("impliedFontSize"), dict) else ts.get("size", 0),
                        "font_type": ts.get("fontStyleName", ""),
                        "color": f"rgb({int(s_color.get('red', 0))},{int(s_color.get('green', 0))},{int(s_color.get('blue', 0))})" if s_color else "",
                    })
                if rich_styles:
                    text_info["rich_styles"] = rich_styles

            return text_info

        return None

    @staticmethod
    def _resolve_targets(designs: list, design_names: Union[str, List[str]]) -> list:
        """Resolve design_names (str or list) to target design dicts."""
        if isinstance(design_names, str) and design_names.lower() == "all":
            return designs

        if isinstance(design_names, str):
            design_names = [design_names]

        seen_ids = set()
        targets = []
        for name in design_names:
            name_str = str(name).strip()
            if name_str.isdigit():
                n = int(name_str)
                for d in designs:
                    if d.get("index") == n and d["id"] not in seen_ids:
                        targets.append(d)
                        seen_ids.add(d["id"])
                        break
            else:
                for d in designs:
                    if d["name"] == name_str and d["id"] not in seen_ids:
                        targets.append(d)
                        seen_ids.add(d["id"])
                        break
        return targets
