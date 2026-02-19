# TaskSum

Azure DevOps の Feature 配下のワークアイテム階層を取得し、工数（見積・残余・完了）を集計・表示する Windows デスクトップアプリです。

## 動作環境

- Windows 10/11
- .NET 8.0 SDK（ビルド時）
- Azure DevOps への読み取りアクセス権を持つ Personal Access Token (PAT)

## ビルド & 実行

```bash
# デバッグビルド
dotnet build

# 実行
dotnet run

# リリースビルド
dotnet build -c Release
```

> このアプリは WPF を使用した **Windows 専用アプリ**です。Linux/macOS ではビルド・実行できません。

---

## 認証設定（PAT の登録）

Azure DevOps への接続には **Personal Access Token (PAT)** を使用します。PAT は次の 2 つの方法で読み込まれ、**環境変数が優先**されます。

### PAT の作成

1. Azure DevOps にサインインし、右上のユーザーアイコン → **Personal access tokens** を選択
2. **New Token** をクリック
3. 以下を設定して作成
   - **Name**: 任意の名前
   - **Scopes**: `Work Items` → **Read**（読み取りのみで動作します）
4. 表示されたトークン文字列をコピーして保存（再表示されません）

---

### 方法 1: 環境変数（推奨）

環境変数に PAT を設定します。環境変数が存在する場合、Windows 資格情報マネージャーよりも**優先**して使用されます。

#### ユーザー環境変数として登録する手順

1. `Win + R` → `sysdm.cpl` → **詳細設定** タブ → **環境変数**
2. 「ユーザー環境変数」の **新規** をクリック
3. 以下を入力して OK
   - **変数名**: `ADO_PAT`（UI の「PAT Key」フィールドで変更可能）
   - **変数値**: コピーした PAT 文字列
4. アプリを再起動して反映

#### コマンドプロンプトで一時的に設定する場合（セッション限定）

```cmd
set ADO_PAT=ここにPATを貼り付け
```

#### PowerShell でユーザー環境変数を永続設定する場合

```powershell
[System.Environment]::SetEnvironmentVariable("ADO_PAT", "ここにPATを貼り付け", "User")
```

---

### 方法 2: Windows 資格情報マネージャー

環境変数が設定されていない場合、アプリは **Windows 資格情報マネージャー** を参照します。P/Invoke (`Advapi32.dll` の `CredReadW`) を使ってネイティブに読み取るため、PAT が平文でファイルに残りません。

#### 登録手順

1. **コントロールパネル** → **資格情報マネージャー** を開く
   （または スタートメニューで「資格情報マネージャー」を検索）
2. **Windows 資格情報** タブを選択
3. **汎用資格情報の追加** をクリック
4. 以下を入力して OK

   | 項目 | 値 |
   |---|---|
   | インターネットまたはネットワークのアドレス | `ADO_PAT` |
   | ユーザー名 | 任意（空欄でも可） |
   | パスワード | コピーした PAT 文字列 |

#### PowerShell で登録する場合

```powershell
$cred = New-Object System.Management.Automation.PSCredential(
    "任意のユーザー名",
    (ConvertTo-SecureString "ここにPATを貼り付け" -AsPlainText -Force)
)
cmdkey /generic:ADO_PAT /user:$cred.UserName /pass:($cred.GetNetworkCredential().Password)
```

---

### PAT キー名の変更

UI の「**PAT Key**」フィールドで、環境変数名および資格情報名を変更できます（デフォルト: `ADO_PAT`）。
複数プロジェクトで異なる PAT を使い分ける場合に利用してください。

---

### 認証フローまとめ

```
アプリ起動 / 読み込みボタン押下
        ↓
環境変数 ${PAT Key} が存在する？
  ├── Yes → その値を PAT として使用
  └── No  → Windows 資格情報マネージャーの汎用資格情報 ${PAT Key} を検索
               ├── 見つかった → その Password を PAT として使用
               └── 見つからない → エラー（"PAT が取得できませんでした" 等）
```

---

## 使い方

1. アプリを起動する
2. 設定バーに以下を入力
   - **Organization URL**: `https://dev.azure.com/yourorg` の形式
   - **Project**: Azure DevOps のプロジェクト名
   - **PAT Key**: 環境変数名 / 資格情報名（デフォルト: `ADO_PAT`）
   - **Feature ID**: 集計対象の Feature ワークアイテム ID
3. **読み込み** ボタンをクリック（または Feature ID 欄で Enter）
4. ツリービューに階層が表示され、画面下部に工数集計テーブルが表示される

### フィルタ機能

| フィルタ | 説明 |
|---|---|
| 担当者 | 特定の担当者のワークアイテムのみ表示 |
| 状態 | 特定の状態（Active / Resolved 等）のみ表示 |
| IsReview | レビュータスクの表示/非表示 |
| Dev.Process | 開発プロセス種別でフィルタ |

- フィルタは「該当するノード、またはその子孫が該当するノード」を表示します
- **フィルタ全解除** ボタンですべてのフィルタをリセットできます

### 集計チェックボックス

ツリーの左端にある「集計」列のチェックボックスにチェックを入れると、チェックしたノードとその子孫のみを集計対象にできます（未チェック時は表示中の全ノードが対象）。

### 集計テーブルの列設定

集計テーブル右上の **列設定** ドロップダウンで、表示する列を選択できます（見積・残余・完了 × 作業/レビュー/全体）。

---

## 設定の保存

Organization URL と Project は `%APPDATA%\TaskSum\settings.json` に自動保存されます。PAT は保存されません。

---

## プロジェクト構成

```
TaskSum/
├── Commands/
│   └── RelayCommand.cs          # RelayCommand / AsyncRelayCommand
├── Converters/                  # WPF 値コンバーター群
├── Models/
│   ├── WorkItemData.cs          # ワークアイテムデータレコード
│   └── AggregationItem.cs       # 集計行モデル
├── Services/
│   ├── AdoService.cs            # ADO REST API 呼び出し（WIQL + バッチ取得）
│   ├── CredentialManagerService.cs  # Windows 資格情報マネージャー読み取り
│   └── SettingsService.cs       # 設定の永続化
├── ViewModels/
│   ├── MainViewModel.cs         # メインロジック（読み込み・ツリー構築・フィルタ・集計）
│   ├── WorkItemNodeViewModel.cs # ツリーノード ViewModel
│   └── FilterOption.cs          # フィルタ選択肢
└── MainWindow.xaml              # メインウィンドウ UI
```
