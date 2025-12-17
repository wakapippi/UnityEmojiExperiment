package com.wakapippi;

import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.util.Log;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileOutputStream;
import java.io.FileReader;
import java.io.FileWriter;
import java.util.ArrayList;
import java.util.List;
import org.json.JSONArray;
import org.json.JSONObject;

public class EmojiAtlas {

    private static final int TILE_SIZE = 34;
    private static final int FONT_SIZE = 28;
    private static final String OUTPUT_IMAGE_NAME = "emoji_atlas.png";
    private static final String OUTPUT_JSON_NAME = "emoji_atlas.json";

    // データ保持用クラス
    private static class EmojiData {
        String charStr;
        String codePoint;
        float x, y;

        EmojiData(String charStr, String codePoint) {
            this.charStr = charStr;
            this.codePoint = codePoint;
        }
    }

    // Unityから呼び出すメソッド
    public static void createAtlas(String inputPath, String outputDir) {
        try {
            List<EmojiData> emojis = loadEmojis(inputPath);
            if (emojis == null || emojis.isEmpty()) {
                Log.e("EmojiAtlas", "絵文字データが見つかりません");
                return;
            }

            int count = emojis.size();
            int columns = (int) Math.ceil(Math.sqrt(count));
            int rows = (int) Math.ceil((double) count / columns);

            int atlasWidth = columns * TILE_SIZE;
            int atlasHeight = rows * TILE_SIZE;

            // ビットマップの作成
            Bitmap bitmap = Bitmap.createBitmap(atlasWidth, atlasHeight, Bitmap.Config.ARGB_8888);
            Canvas canvas = new Canvas(bitmap);
            
            // 描画設定
            Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
            paint.setTextSize(FONT_SIZE);
            paint.setColor(Color.BLACK); 
            paint.setTextAlign(Paint.Align.CENTER);
            
            // 背景クリア
            canvas.drawColor(Color.TRANSPARENT);

            Paint.FontMetrics metrics = paint.getFontMetrics();
            float textHeightOffset = (metrics.descent + metrics.ascent) / 2;

            List<EmojiData> validFrames = new ArrayList<>();
            int validCount = 0;

            for (int i = 0; i < count; i++) {
                EmojiData item = emojis.get(i);

                float textWidth = paint.measureText(item.charStr);
                
                if (textWidth > TILE_SIZE) {
                    Log.d("EmojiAtlas", "未対応の絵文字: " + item.charStr);
                    continue;
                }

                int col = validCount % columns;
                int row = validCount / columns;

                float x = col * TILE_SIZE;
                float y = row * TILE_SIZE;
                
                // データの座標を更新
                item.x = x;
                item.y = y;

                float centerX = x + (TILE_SIZE / 2f);
                float centerY = y + (TILE_SIZE / 2f);
                
                canvas.drawText(item.charStr, centerX, centerY - textHeightOffset, paint);
                
                validFrames.add(item);
                validCount++;
            }
            save(bitmap, validFrames, outputDir);
            
            if (!bitmap.isRecycled()) {
                bitmap.recycle();
            }

        } catch (Exception e) {
            Log.e("EmojiAtlas", "エラー発生: " + e.getMessage());
            e.printStackTrace();
        }
    }

    private static List<EmojiData> loadEmojis(String path) {
        File file = new File(path);
        if (!file.exists()) return null;
        List<EmojiData> validEmojis = new ArrayList<>();
        try (BufferedReader br = new BufferedReader(new FileReader(file))) {
            String line;
            while ((line = br.readLine()) != null) {
                String trimmed = line.trim();
                if (trimmed.isEmpty() || trimmed.startsWith("#")) continue;
                String[] parts = trimmed.split(";");
                if (parts.length == 0) continue;
                String codePointPart = parts[0].trim();
                String emoji = parseCodePoints(codePointPart);
                if (emoji != null && !emoji.isEmpty()) {
                    validEmojis.add(new EmojiData(emoji, codePointPart));
                }
            }
        } catch (Exception e) { return null; }
        return validEmojis;
    }

    private static String parseCodePoints(String rawString) {
        try {
            String[] hexParts = rawString.trim().split("\\s+");
            StringBuilder sb = new StringBuilder();
            for (String hex : hexParts) {
                if (hex.isEmpty()) continue;
                int code = Integer.parseInt(hex, 16);
                sb.append(new String(Character.toChars(code)));
            }
            return sb.toString();
        } catch (Exception e) { return null; }
    }

    private static void save(Bitmap bitmap, List<EmojiData> frames, String outputDir) throws Exception {
        File dir = new File(outputDir);
        if (!dir.exists()) dir.mkdirs();

        File imageFile = new File(dir, OUTPUT_IMAGE_NAME);
        try (FileOutputStream out = new FileOutputStream(imageFile)) {
            bitmap.compress(Bitmap.CompressFormat.PNG, 100, out);
        }

        JSONObject root = new JSONObject();
        root.put("imageName", OUTPUT_IMAGE_NAME);

        JSONArray frameArray = new JSONArray();
        for (EmojiData f : frames) {
            JSONObject frameObj = new JSONObject();
            frameObj.put("name", f.charStr);
            frameObj.put("codePoint", f.codePoint);

            JSONObject rect = new JSONObject();
            rect.put("x", (double)f.x);
            rect.put("y", (double)f.y);
            rect.put("w", (double)TILE_SIZE);
            rect.put("h", (double)TILE_SIZE);
            
            frameObj.put("frame", rect);
            frameArray.put(frameObj);
        }
        root.put("frames", frameArray);

        File jsonFile = new File(dir, OUTPUT_JSON_NAME);
        try (FileWriter writer = new FileWriter(jsonFile)) {
            writer.write(root.toString(2));
        }
    }
}