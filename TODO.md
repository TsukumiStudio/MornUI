# MornUI - AIフレンドリーなUI構築ライブラリ

## コンセプト

AIが親しんでいるHTML/CSS/JSの構文をそのまま使ってUnity上でUIを構築・レンダリングするライブラリ。
AIに「HTMLでUI作って」と言えば、そのまま動くUIが出来上がる世界を目指す。

## アーキテクチャ

```
.html / .css / .js ファイル (StreamingAssets等)
        │
   ┌────┴────┐
   │ HTML    │ CSS     │
   │ Parser  │ Parser  │
   └────┬────┘
        │
   [DOM Tree + CSSOM]
        │
   [Layout Engine (Flexbox)]
        │
   [Renderer → Texture2D]
        │
   [RawImage on uGUI Canvas]
        │
   [Input: 座標ヒットテスト]
```

### 技術選定

| 要素 | 選定 | 理由 |
|------|------|------|
| レンダリング | RawImage + Texture2D独自描画 | AIはHTML/CSSだけ触ればよく、Unity側の知識不要 |
| HTMLパース | AngleSharp or 自作 | C#製OSSでDOM操作も対応 |
| CSSパース | 自作（ゲームUI特化） | フルCSS3は不要。Flexbox + 基本プロパティに絞る |
| JSエンジン | Jint (NuGet) | C#純正JS interpreter。全プラットフォーム対応。ES6+ |
| フォント | TextMeshPro → RenderTexture → Texture2D合成 | Unity既存機能を再利用 |
| 入力 | RawImage上のUV座標 → レイアウトRect照合 | マウス・タッチ・コントローラー全対応 |
| アニメーション | CSS (transition + transform + @keyframes) + JS (requestAnimationFrame) | CSSで宣言的に書くのが最もAIフレンドリー。JSでも制御可能 |
| プレビュー | Editorウィンドウ | 単体で見た目確認・ホットリロード可能 |
| プラットフォーム | マルチ (PC / Mobile / Console) | ネイティブ依存なし |

### サポートするCSSプロパティ

```css
/* レイアウト */
display: flex | none
flex-direction: row | column
justify-content: flex-start | flex-end | center | space-between | space-around
align-items: flex-start | flex-end | center | stretch
flex-grow / flex-shrink / flex-basis
width / height / min-width / max-width / min-height / max-height
margin / padding (top, right, bottom, left)
position: relative | absolute
top / right / bottom / left
overflow: visible | hidden | scroll
gap

/* 見た目 */
color
background-color
background-image: url(...)
opacity
border: width style color
border-radius
box-shadow (簡易版)

/* テキスト */
font-size
font-family
font-weight: normal | bold
text-align: left | center | right
line-height
white-space: normal | nowrap
text-overflow: ellipsis

/* トランスフォーム */
transform: scale(x) / scale(x, y)
transform: translateX(px) / translateY(px)
transform: rotate(deg)
transform: 複数指定 (e.g. scale(1.1) translateX(10px))
transform-origin

/* トランジション */
transition: property duration timing-function
transition-property
transition-duration
transition-timing-function: ease | linear | ease-in | ease-out | ease-in-out
transition-delay

/* アニメーション */
@keyframes name { from {} to {} / 0% {} 50% {} 100% {} }
animation: name duration timing-function delay iteration-count direction fill-mode
animation-name
animation-duration
animation-timing-function
animation-delay
animation-iteration-count: number | infinite
animation-direction: normal | reverse | alternate
animation-fill-mode: none | forwards | backwards | both

/* その他 */
cursor: pointer (フォーカス可能要素の判別に使用)
z-index
```

### サポートするHTML要素

```html
<div>       <!-- 汎用コンテナ -->
<span>      <!-- インラインテキスト -->
<p>         <!-- 段落 -->
<button>    <!-- ボタン（クリック/フォーカス可能） -->
<img>       <!-- 画像 -->
<input>     <!-- テキスト入力（type="text"） -->
<select>    <!-- ドロップダウン -->
<ul> <ol> <li>  <!-- リスト -->
<h1>~<h6>   <!-- 見出し -->
<a>         <!-- リンク（onclick扱い） -->
```

### サポートするJS API (DOM)

```javascript
// 要素取得
document.getElementById(id)
document.querySelector(selector)
document.querySelectorAll(selector)

// 要素操作
element.textContent
element.innerHTML
element.style.property = value
element.classList.add / remove / toggle / contains
element.setAttribute / getAttribute / removeAttribute
element.appendChild(child)
element.removeChild(child)
element.insertBefore(newNode, referenceNode)
document.createElement(tagName)

// イベント
element.addEventListener(type, handler)
element.removeEventListener(type, handler)
// サポートするイベント: click, focus, blur, input, change

// アニメーション
requestAnimationFrame(callback)
cancelAnimationFrame(id)

// タイマー
setTimeout / clearTimeout
setInterval / clearInterval

// C# ブリッジ
MornUI.call(methodName, ...args)  // C#側のメソッドを呼ぶ
MornUI.on(eventName, handler)     // C#側からのイベントを受け取る
```

---

## 実装フェーズ

### Phase 0: 基盤 (Foundation)

- [x] **P0-1: プロジェクト構造**
  - MornUI.asmdef 作成
  - フォルダ構成: Core/, Parser/, Layout/, Renderer/, Input/, Editor/, Runtime/
  - 依存パッケージの導入 (Jint, AngleSharp検討)

- [x] **P0-2: HTMLパーサー**
  - HTMLテキスト → DOMツリー構築
  - サポート要素: div, span, p, button, img
  - 属性: id, class, style, onclick
  - AngleSharpを使うか自作するか評価・決定

- [x] **P0-3: CSSパーサー**
  - CSSテキスト → ルールセット(セレクタ + プロパティ)
  - インラインstyle属性のパース
  - セレクタ: タグ名, #id, .class, 子孫セレクタ, 直下子セレクタ
  - プロパティ値のパース (px, %, 色, etc.)

- [x] **P0-4: CSSOMとスタイル計算**
  - セレクタの詳細度 (specificity) 計算
  - カスケーディング: インラインstyle > id > class > タグ
  - 継承プロパティの伝播 (color, font-size, etc.)
  - 計算済みスタイル (ComputedStyle) の算出

### Phase 1: レイアウトエンジン (Layout)

- [x] **P1-1: ボックスモデル**
  - content / padding / border / margin の計算
  - width / height の解決 (px, %, auto)
  - min-width / max-width / min-height / max-height

- [x] **P1-2: Flexboxレイアウト**
  - flex-direction: row | column
  - justify-content: flex-start | flex-end | center | space-between | space-around
  - align-items: flex-start | flex-end | center | stretch
  - flex-grow / flex-shrink / flex-basis
  - gap
  - flex-wrap (stretch対応)

- [ ] **P1-3: Positioning**
  - position: relative → 通常フローからのオフセット
  - position: absolute → 親の位置基準で配置
  - top / right / bottom / left
  - z-index による描画順序

- [x] **P1-4: テキストレイアウト**
  - [x] テキストの行分割 (word wrap)
  - [x] text-align: left | center | right
  - [x] line-height
  - [x] white-space: normal | nowrap
  - [x] text-overflow: ellipsis
  - [ ] インライン要素の並び (span等、将来対応)

- [ ] **P1-5: overflow**
  - [x] overflow: hidden → クリッピング
  - [ ] overflow: scroll → スクロール領域
  - [ ] スクロール位置の管理

### Phase 2: レンダラー (Renderer)

- [x] **P2-1: 基本図形描画**
  - [x] Texture2Dへの矩形塗りつぶし (background-color)
  - [x] border描画 (実線のみ)
  - [x] border-radius (角丸)
  - [x] opacity

- [x] **P2-2: テキスト描画**
  - TMP_FontAssetのSDFアトラスから直接描画
  - font-size / color 対応
  - text-align: left | center | right

- [ ] **P2-3: 画像描画**
  - `<img src="...">` の画像読み込み (Resources / StreamingAssets)
  - background-image 対応
  - アスペクト比の維持 / 引き伸ばし

- [ ] **P2-4: 描画最適化**
  - ダーティフラグ（変更箇所のみ再描画）
  - レイアウトキャッシュ
  - テキスト描画のキャッシュ
  - RawImage更新の最適化

- [ ] **P2-5: transform描画**
  - [x] transform: scale → 拡大縮小描画
  - [x] transform: translate → オフセット描画
  - [ ] transform: rotate → 回転描画
  - [ ] transform-origin の適用
  - [x] 複数transform の合成

- [ ] **P2-6: box-shadow**
  - 簡易的なドロップシャドウ
  - ぼかし処理

### Phase 2.5: CSSアニメーション (Animation)

- [x] **P2A-1: transition**
  - [x] プロパティ変更の検知 (スタイル再計算時に前回値と比較)
  - [x] 補間可能プロパティの判定 (opacity, background-color, color, border-color, border-radius, transform)
  - [x] イージング関数の実装 (ease, linear, ease-in, ease-out, ease-in-out)
  - [x] transition-delay 対応
  - [x] 複数プロパティの同時transition

- [x] **P2A-2: @keyframes + animation**
  - [x] @keyframesルールのパース (from/to, パーセンテージ)
  - [x] キーフレーム間の補間
  - [x] animation-iteration-count (回数指定 / infinite)
  - [x] animation-direction (normal / reverse / alternate)
  - [x] animation-fill-mode (none / forwards / backwards / both)
  - [x] animation-delay
  - [x] 複数アニメーションの同時実行

- [x] **P2A-3: アニメーションエンジン**
  - [x] Unity Update → アニメーションフレーム更新
  - [x] 実行中アニメーションの管理 (追加/削除/一時停止)
  - [ ] アニメーション完了時のコールバック (animationend, transitionend イベント)
  - [x] パフォーマンス: ダーティフラグとの連携 (アニメ中は毎フレーム再描画)

### Phase 3: JavaScript統合 (JS Engine)

- [x] **P3-1: Jint統合**
  - Jint Engine のセットアップ
  - .jsファイルの読み込みと実行
  - エラーハンドリングとログ出力

- [x] **P3-2: DOM APIエミュレーション** (部分実装)
  - document オブジェクトのプロキシ実装
  - getElementById / querySelector / querySelectorAll
  - [x] textContent / style プロパティ
  - [x] classList (add / remove / toggle / contains)
  - [x] setAttribute / getAttribute
  - [ ] createElement / appendChild / removeChild
  - [ ] innerHTML

- [x] **P3-3: イベントシステム**
  - addEventListener / removeEventListener
  - onclick属性からのイベント登録
  - イベントバブリング
  - サポートイベント: click (focus, blur, input, changeは未実装)

- [ ] **P3-4: タイマーとアニメーション**
  - requestAnimationFrame (Unity Update連動)
  - cancelAnimationFrame
  - setTimeout / clearTimeout
  - setInterval / clearInterval

- [x] **P3-5: C#ブリッジ** (部分実装)
  - MornUI.call(methodName, ...args) → C#メソッド呼び出し
  - [ ] MornUI.on(eventName, handler) → C#からJSへイベント発火
  - [ ] 型変換 (JS ↔ C#)

### Phase 4: 入力システム (Input)

- [x] **P4-1: マウス/タッチ入力**
  - RawImage上のクリック座標 → UV座標変換
  - UV座標 → レイアウト座標変換
  - ヒットテスト (z-indexとoverflow考慮)
  - click イベント発火 (hover / pressは未実装)

- [ ] **P4-2: フォーカスシステム**
  - [x] Tab / Shift+Tabでフォーカス移動
  - [x] フォーカス順序の自動計算 (tabindex対応)
  - [x] :focus 疑似クラスによるスタイル変更
  - [ ] フォーカスリング描画

- [ ] **P4-3: コントローラー入力**
  - InputSystem連携
  - Navigate入力 → フォーカス移動
  - Submit入力 → click発火
  - Cancel入力 → イベント発火
  - フォーカス可能要素間のナビゲーション (上下左右)

- [ ] **P4-4: テキスト入力**
  - `<input type="text">` のテキスト入力対応
  - IME対応 (日本語入力)
  - カーソル表示
  - 選択 / コピー / ペースト

- [ ] **P4-5: スクロール入力**
  - マウスホイール / タッチスワイプ → overflow: scroll 領域のスクロール
  - スクロールバー表示
  - 慣性スクロール

### Phase 5: Editorプレビュー (Editor)

- [x] **P5-1: プレビューウィンドウ**
  - EditorWindowでレンダリング結果を表示
  - ウィンドウリサイズ対応
  - HTMLファイル選択UI
  - ブラウザスクリーンショット比較機能

- [x] **P5-2: ホットリロード**
  - FileSystemWatcherで.html/.cssの変更を検知
  - 変更時に自動で再パース・再レンダリング

- [ ] **P5-3: インスペクター**
  - 要素ツリーの表示 (Chrome DevTools風)
  - 要素選択 → プロパティ表示
  - 計算済みスタイルの表示
  - ボックスモデルの可視化

- [ ] **P5-4: コンソール**
  - console.log / warn / error の表示
  - JSエラーの表示
  - 簡易JS実行

### Phase 6: ランタイム統合 (Runtime)

- [ ] **P6-1: MornUIコンポーネント**
  - MonoBehaviourコンポーネント
  - HTMLファイルパス指定
  - 自動的にRawImage + Canvas構成を生成
  - 解像度設定

- [ ] **P6-2: C# API**
  ```csharp
  // UI生成
  var ui = MornUIRuntime.Create(htmlPath, cssPath);

  // JS実行
  ui.ExecuteJS("updateHP(80)");

  // C#メソッドをJSに公開
  ui.RegisterMethod("OnSubmit", () => { /* ... */ });

  // JSからのイベント受信
  ui.On("itemSelected", (args) => { /* ... */ });

  // 値のバインド
  ui.SetValue("player.name", playerName);
  ```

- [ ] **P6-3: リソース管理**
  - 画像の読み込みパス解決 (Resources / StreamingAssets / Addressables)
  - フォントの管理 (TMP_FontAsset)
  - CSSファイルのインポート (@import)

- [ ] **P6-4: ライフサイクル**
  - UI表示 / 非表示
  - Destroyによるリソース解放
  - シーン遷移時のクリーンアップ

---

## 使用例

### メニュー画面

```html
<!-- menu.html -->
<div class="menu-container">
  <h1 class="title">My Game</h1>
  <div class="button-list">
    <button class="menu-btn" onclick="MornUI.call('StartGame')">
      はじめる
    </button>
    <button class="menu-btn" onclick="MornUI.call('OpenSettings')">
      せってい
    </button>
    <button class="menu-btn" onclick="MornUI.call('QuitGame')">
      おわる
    </button>
  </div>
</div>
```

```css
/* menu.css */
.menu-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  background-color: #1a1a2e;
}

.title {
  font-size: 64px;
  color: #e94560;
  margin-bottom: 60px;
}

.button-list {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.menu-btn {
  width: 300px;
  padding: 15px 0;
  font-size: 28px;
  color: #ffffff;
  background-color: #16213e;
  border: 2px solid #0f3460;
  border-radius: 8px;
  cursor: pointer;
}

.menu-btn:focus {
  background-color: #0f3460;
  border-color: #e94560;
  transform: scale(1.08);
}

/* ボタン出現アニメーション */
@keyframes fadeSlideIn {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.menu-btn {
  animation: fadeSlideIn 0.3s ease forwards;
}

.menu-btn:nth-child(2) { animation-delay: 0.1s; }
.menu-btn:nth-child(3) { animation-delay: 0.2s; }
```

### HPバー (HUD)

```html
<!-- hud.html -->
<div class="hud">
  <div class="hp-container">
    <div class="hp-bar" id="hp-bar"></div>
    <span class="hp-text" id="hp-text">100 / 100</span>
  </div>
</div>
```

```javascript
// hud.js
MornUI.on('updateHP', function(current, max) {
  var bar = document.getElementById('hp-bar');
  var text = document.getElementById('hp-text');
  var ratio = current / max;
  bar.style.width = (ratio * 100) + '%';
  bar.style.backgroundColor = ratio > 0.5 ? '#4caf50' : ratio > 0.2 ? '#ff9800' : '#f44336';
  text.textContent = current + ' / ' + max;
});
```

---

## 未決定事項

- [ ] AngleSharp vs 自作HTMLパーサーの最終判断
  - AngleSharp: 完成度高いがサイズ大。NuGetで入る
  - 自作: サポート要素が少ないので自作も現実的
- [ ] Jintのパフォーマンス評価 (モバイル含む)
- [ ] フォント描画のパフォーマンス最適化手法
- [ ] 画像キャッシュの戦略
- [ ] CSSの `:hover` / `:focus` / `:active` / `:nth-child()` 疑似クラスのサポート範囲
- [ ] `<select>` のドロップダウン表示方法
- [ ] テキスト入力時のIME処理方法

---

## 開発優先順位

```
Phase 0   (基盤)          ━━━━━━━━━ 最初に全部
Phase 1   (レイアウト)    ━━━━━━━━━ P1-1 → P1-2 → P1-4 → P1-3 → P1-5
Phase 2   (レンダラー)    ━━━━━━━━━ P2-1 → P2-2 → P2-3 → P2-5 → P2-4 → P2-6
Phase 2.5 (CSSアニメ)     ━━━━━━━━━ P2A-1 → P2A-2 → P2A-3
Phase 3   (JS統合)        ━━━━━━━━━ P3-1 → P3-2 → P3-3 → P3-5 → P3-4
Phase 5   (Editor)        ━━━━━━━━━ P5-1 → P5-2 (ここまで早めに)
Phase 4   (入力)          ━━━━━━━━━ P4-1 → P4-2 → P4-3 → P4-4 → P4-5
Phase 6   (ランタイム)    ━━━━━━━━━ P6-1 → P6-2 → P6-3 → P6-4
Phase 5   (Editor続)      ━━━━━━━━━ P5-3 → P5-4
```

## マイルストーン

1. **M1: 矩形が表示される** — Phase 0 + P1-1 + P2-1 ✅
   - HTML/CSSをパースしてdivの背景色が描画される
2. **M2: テキスト付きレイアウト** — P1-2 + P1-4 + P2-2 ✅
   - Flexboxで並んだボタンにテキストが表示される
3. **M3: Editorで確認できる** — P5-1 + P5-2 ✅
   - HTMLを変更すると即座にEditorウィンドウに反映
4. **M4: クリックできる** — P3-1 + P3-3 + P4-1 ✅
   - ボタンをクリックしてJSのonclickが発火する
5. **M5: アニメーションが動く** — P2-5 + P2A-1 + P2A-2 + P2A-3 ✅
   - CSS transition/transform/@keyframesでボタンがアニメーションする
6. **M6: メニュー画面が動く** — P3-2 + P3-5 + P4-2
   - 使用例のメニュー画面がそのまま動作する（CSSアニメーション付き）
7. **M7: HUDが動く** — P3-4 + P6-1 + P6-2
   - HPバーがJS経由でアニメーションする
