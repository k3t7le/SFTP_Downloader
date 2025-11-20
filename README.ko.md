# SFTP Downloader

여러 SFTP 폴더를 잡(Job) 단위로 안전하게 내려받고, 필요 시 원격 삭제 또는 로컬 아카이브까지 처리하는 .NET 8 콘솔 앱입니다. 다운로드는 항상 `.part`로 진행해 무결성을 보장하고, 실행 결과는 콘솔 요약과 파일 로그로 남습니다.

## 주요 기능

- **잡 기반 설정** — 잡마다 원격 폴더와 독립된 로컬 대상/아카이브 경로를 매핑합니다.
- **원자적 다운로드** — `<이름>.part`로 받은 후 완료 시 최종 이름으로 승격합니다.
- **파일 단위 격리** — 한 파일 실패가 다른 파일에 영향을 주지 않고 요약에 집계됩니다.
- **아카이브 스테이징** — `%TEMP%/sftp-downloader/archive-temp`에서 `tar.gz`로 만든 뒤 `ArchiveFolder`로 원자적 이동, 시작 시 남은 `.tmp`는 자동 정리됩니다.
- **로그/요약** — 제한된 콘솔 진행률, 디버그까지 담는 롤링 로그, `RUN-SUMMARY …` 한 줄로 grep 가능한 실행 요약을 제공합니다.

## 빠른 시작

1. [.NET 8 SDK](https://dotnet.microsoft.com/) 설치
2. `appsettings.json`에 SFTP 정보, 로그 경로, 잡 설정 입력
3. 빌드

   ```bash
   dotnet restore
   dotnet build SFTP_Downloader.sln
   ```

4. 실행

   ```bash
   dotnet run --project SFTP_Downloader.csproj -- --job AGING
   ```

   - `--job` 없이 실행하면 모든 잡 수행
   - 여러 잡 필터: `--job` 반복 또는 `--jobs=AGING,CONFIRM`
   - `Ctrl+C`로 중단 시 안전하게 종료 후 요약 출력

## 설정 (`appsettings.json`)

- `Sftp`: `Host`, `Port`, `Username`, `Password` 또는 `PrivateKeyPath`/`PrivateKeyPassphrase`
- `Jobs[]`:
  - `Name` — 잡 이름
  - `RemoteFolders` — 원격 폴더 배열
  - `LocalTargetFolder` — 로컬 저장 경로(잡별 고유)
  - `SearchPattern` — 예: `"*.dat"`
  - `DeleteRemoteAfterSuccess` — 성공 후 원격 삭제 여부
  - `ArchiveFolder` — `tar.gz` 아카이브 저장 경로(생성은 temp에서, 이동 후 완료)
- `Logging`: `LogFolder`, `RetentionDays`

## 로그와 요약

- 콘솔: 폴더당 최대 약 10줄 진행률 + ASCII 요약
- 파일: `sftp-downloader-*.log`(정보/디버그), `RUN-SUMMARY total:succ=...` 포함

## 문서

- 사용 방법: `docs/Usage.ko.md` / `docs/Usage.md`
- 아키텍처: `docs/Arch.ko.md` / `docs/Arch.md`
- 개념 노트: `concept.md`

## 테스트

- 예정된 xUnit 테스트는 `tests/SFTP_Downloader.Tests`에 추가 예정이며, 준비되면 `dotnet test`로 실행합니다.
