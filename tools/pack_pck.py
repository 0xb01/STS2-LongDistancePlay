import hashlib
import os
import struct
import sys

def pack_pck(output_pck_path, root_dir):
    ctex_dest = os.path.join(root_dir, ".godot", "imported", "mod_image.png-4eb23a2e47c441630339d5c501192f82.ctex")
    ctex_fallback = os.path.join(root_dir, "tools", "mod_image.ctex")

    img_dir = os.path.join(root_dir, "STS2-LongDistancePlay")
    png_path = os.path.join(img_dir, "mod_image.png")
    jpg_path = os.path.join(img_dir, "mod_image.jpg")
    if not os.path.exists(jpg_path):
        jpg_path = os.path.join(img_dir, "mod_image.jpeg")

    # If mod_image not in img_dir, check src
    if not os.path.exists(png_path) and not os.path.exists(jpg_path):
        alt_img_dir = os.path.join(root_dir, "src", "STS2-LongDistancePlay")
        if os.path.exists(os.path.join(alt_img_dir, "mod_image.png")):
            png_path = os.path.join(alt_img_dir, "mod_image.png")
        if os.path.exists(os.path.join(alt_img_dir, "mod_image.jpg")):
            jpg_path = os.path.join(alt_img_dir, "mod_image.jpg")

    if os.path.exists(jpg_path) and (not os.path.exists(png_path) or os.path.getmtime(jpg_path) > os.path.getmtime(png_path)):
        try:
            from PIL import Image
            im = Image.open(jpg_path).convert("RGBA")
            im = im.resize((1024, 572), Image.Resampling.LANCZOS)
            im.save(png_path, format="PNG")
        except Exception as e:
            print(f"[Warning] Could not auto-convert {jpg_path} to PNG: {e}")

    # Regenerate Godot StreamTexture (.ctex) if PNG is newer or ctex is missing
    if os.path.exists(png_path) and (not os.path.exists(ctex_dest) or os.path.getmtime(png_path) > os.path.getmtime(ctex_dest)):
        try:
            from PIL import Image
            import io
            im = Image.open(png_path).convert("RGBA")
            buf = io.BytesIO()
            im.save(buf, format="WEBP", lossless=True)
            webp_bytes = buf.getvalue()
            w, h = im.size
            header = bytearray(56)
            header[0:4] = b"GST2"
            struct.pack_into("<IIII", header, 4, 1, w, h, 0x0D000000)
            struct.pack_into("<I", header, 20, 0xFFFFFFFF)
            struct.pack_into("<IHHII", header, 36, 2, w, h, 0, 4)
            struct.pack_into("<I", header, 52, len(webp_bytes))
            os.makedirs(os.path.dirname(ctex_dest), exist_ok=True)
            with open(ctex_dest, "wb") as fh:
                fh.write(bytes(header) + webp_bytes)
        except Exception as e:
            print(f"[Warning] Could not generate CTEX from PNG: {e}")

    if not os.path.exists(ctex_dest) and os.path.exists(ctex_fallback):
        os.makedirs(os.path.dirname(ctex_dest), exist_ok=True)
        import shutil
        shutil.copy2(ctex_fallback, ctex_dest)

    # Manifest file
    manifest_path = os.path.join(root_dir, "STS2-LongDistancePlay.json")
    if not os.path.exists(manifest_path):
        manifest_path = os.path.join(root_dir, "src", "STS2-LongDistancePlay", "STS2-LongDistancePlay.json")

    def make_import_content(source_res_path):
        return (
            '[remap]\n\n'
            'importer="texture"\n'
            'type="CompressedTexture2D"\n'
            'path="res://.godot/imported/mod_image.png-4eb23a2e47c441630339d5c501192f82.ctex"\n'
            'metadata={\n"vram_texture": false\n}\n\n'
            '[deps]\n\n'
            f'source_file="{source_res_path}"\n'
            'dest_files=["res://.godot/imported/mod_image.png-4eb23a2e47c441630339d5c501192f82.ctex"]\n\n'
            '[params]\n\n'
            'compress/mode=0\n'
            'compress/high_quality=false\n'
            'compress/lossy_quality=0.7\n'
            'compress/uastc_level=0\n'
            'compress/rdo_quality_loss=0.0\n'
            'compress/hdr_compression=1\n'
            'compress/normal_map=0\n'
            'compress/channel_pack=0\n'
            'mipmaps/generate=false\n'
            'mipmaps/limit=-1\n'
            'roughness/mode=0\n'
            'roughness/src_normal=""\n'
            'process/channel_remap/red=0\n'
            'process/channel_remap/green=1\n'
            'process/channel_remap/blue=2\n'
            'process/channel_remap/alpha=3\n'
            'process/fix_alpha_border=true\n'
            'process/premult_alpha=false\n'
            'process/normal_map_invert_y=false\n'
            'process/hdr_as_srgb=false\n'
            'process/hdr_clamp_exposure=false\n'
            'process/size_limit=0\n'
            'detect_3d/compress_to=1\n'
        ).encode("utf-8")

    files_to_pack = [
        ("res://STS2-LongDistancePlay.json", manifest_path),
        ("res://mod_manifest.json", manifest_path),
        ("res://STS2-LongDistancePlay/mod_image.png", png_path),
        ("res://Long Distance Play/mod_image.png", png_path),
        ("res://.godot/imported/mod_image.png-4eb23a2e47c441630339d5c501192f82.ctex", ctex_dest),
    ]

    virtual_files = [
        ("res://STS2-LongDistancePlay/mod_image.png.import", make_import_content("res://STS2-LongDistancePlay/mod_image.png")),
        ("res://Long Distance Play/mod_image.png.import", make_import_content("res://Long Distance Play/mod_image.png")),
    ]

    file_entries = []
    file_data_blobs = []
    current_offset = 0

    def add_entry(rel_path, content_bytes):
        nonlocal current_offset
        size = len(content_bytes)
        md5 = hashlib.md5(content_bytes).digest()
        file_entries.append((rel_path, current_offset, size, md5))
        file_data_blobs.append(content_bytes)
        # 16-byte alignment
        current_offset += size
        pad = (16 - (current_offset % 16)) % 16
        if pad > 0:
            file_data_blobs.append(b"\x00" * pad)
            current_offset += pad

    for rel_path, full_path in files_to_pack:
        if not os.path.exists(full_path):
            raise FileNotFoundError(f"Missing required file: {full_path}")
        with open(full_path, "rb") as fh:
            add_entry(rel_path, fh.read())

    for rel_path, content in virtual_files:
        add_entry(rel_path, content)

    os.makedirs(os.path.dirname(output_pck_path), exist_ok=True)

    header = bytearray(128)
    # Magic GDPC
    header[0:4] = b"GDPC"
    # Format 3, Godot 4.5.1
    struct.pack_into("<IIII", header, 4, 3, 4, 5, 1)
    # Flags = 2 (PCK_FLAG_DIR_AT_END)
    struct.pack_into("<I", header, 20, 2)
    # File base = 128
    struct.pack_into("<Q", header, 24, 128)
    # Dir offset = 128 + current_offset
    dir_offset = 128 + current_offset
    struct.pack_into("<Q", header, 32, dir_offset)

    dir_buffer = bytearray()
    dir_buffer.extend(struct.pack("<I", len(file_entries)))

    for rel_path, offset, size, md5 in file_entries:
        path_bytes = rel_path.encode("utf-8")
        pad_len = (4 - (len(path_bytes) % 4)) % 4
        if pad_len > 0:
            path_bytes += b"\x00" * pad_len
        dir_buffer.extend(struct.pack("<I", len(path_bytes)))
        dir_buffer.extend(path_bytes)
        dir_buffer.extend(struct.pack("<QQ", offset, size))
        dir_buffer.extend(md5)
        dir_buffer.extend(struct.pack("<I", 0))

    with open(output_pck_path, "wb") as out:
        out.write(header)
        for blob in file_data_blobs:
            out.write(blob)
        out.write(dir_buffer)

    print(f"Successfully generated {output_pck_path} with {len(file_entries)} files ({os.path.getsize(output_pck_path)} bytes).")

if __name__ == "__main__":
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    out = os.path.join(root, "build", "STS2-LongDistancePlay.pck")
    if len(sys.argv) > 1:
        out = sys.argv[1]
    pack_pck(out, root)
