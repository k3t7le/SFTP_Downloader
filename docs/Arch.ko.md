# 아키텍처

이 문서는 SFTP Downloader의 구조를 설명합니다.

## 1. 전체 흐름

```
Program.cs
   └─ Host + DI 컨테이너
        ├─ CliOptions (잡 필터)
        ├─ IOptions<AppSettings> (환경설정)
        ├─ SftpClientFactory (SSH.NET 연결)
        └─ SftpDownloadApplication
                └─ JobProcessor
                        └─ SftpClient (Renci.SshNet)
```

`Program`이 Serilog와 의존성 주입을 구성한 뒤 `SftpDownloadApplication`을 실행합니다. 이 클래스는 실행할 잡 목록을 결정하고 단일 `SftpClient`를 생성한 뒤 실제 다운로드 로직을 `JobProcessor`에 위임합니다.

## 2. 설정 모델

- `Configuration/AppSettings.cs`는 세 가지 옵션 클래스를 정의합니다.
  - `SftpOptions`: 호스트, 포트, 계정, 비밀번호/키 정보.
  - `JobOptions`: 잡 이름, 원격 폴더 목록, 로컬 저장 경로, 검색 패턴, 원격 삭제/아카이브 설정.
  - `LoggingOptions`: 로그 폴더 경로와 보존 일수.
- `appsettings.json`은 솔루션 루트에 있고 빌드 시 출력 폴더로 복사되므로 실행 환경에서도 동일하게 사용됩니다.

## 3. CLI 필터링

- `Cli/CliOptions`가 `--job NAME`, 다중 `--job`, `--jobs=NAME1,NAME2` 인자를 파싱합니다.
- 필터가 없으면 모든 잡을 실행하고, 지정되면 해당 이름의 잡만 수행합니다.

## 4. SFTP 연결

- `Sftp/SftpClientFactory`가 `Renci.SshNet.SftpClient`를 생성/연결합니다.
- 프로그램 전체에서 하나의 클라이언트를 재사용해 불필요한 재연결을 줄입니다.

## 5. Job 실행 파이프라인

`JobProcessor.ProcessJob`의 주요 단계는 다음과 같습니다.

1. 잡에 원격 폴더가 있는지 확인하고 로컬 대상 폴더를 생성합니다.
2. 각 원격 폴더를 순회하며 `ListDirectory`로 파일 목록을 가져옵니다.
3. `SearchPattern`으로 필터링된 후보 리스트를 만듭니다.
4. 후보 파일마다:
   - `<LocalTargetFolder>/<이름>.part` 파일로 다운로드
   - 완료 후 크기를 비교하여 무결성 확인
   - `.part`를 실제 파일명으로 rename (원자적 승격)
   - `DeleteRemoteAfterSuccess`가 true면 원격 파일 삭제
   - 예외가 발생해도 해당 파일만 실패 처리하고 다음 파일로 진행
5. 폴더 처리가 끝나면 `ArchiveFolder` 설정이 있을 경우 로컬 폴더를 `.gz`로 압축 후 비웁니다.

실행 결과는 `JobRunResult`에 담기며, 각 폴더별 `FolderRunResult`(전체/성공/실패/시간/실패 파일 목록)를 포함합니다.

## 6. 로깅 및 요약

- `Program.ConfigureSerilog`는 콘솔(LogLevel.Information 이상)과 파일(LogLevel.Debug 이상) 두 개의 sink를 설정합니다.
- `JobProcessor`는 다운로드 세부 단계는 Debug 로그로 남기고, 콘솔에는 폴더당 최대 10회만 진행률을 출력합니다.
- 모든 잡이 끝나면 `JobSummaryPrinter`가 ASCII 배너를 콘솔과 로그 파일 모두에 남겨 보고용 데이터를 일원화합니다.

## 7. 오류 처리

- `Ctrl+C` 입력 시 `CancellationToken`을 통해 안전하게 중단하고 종료 코드 2를 반환합니다.
- 처리되지 않은 예외는 최상위에서 잡혀 종료 코드 1과 함께 로그됩니다.
- 개별 파일 실패는 목록에 기록되어 전체 잡이 중단되지 않으며, 운영자가 실패 파일만 재처리하기 쉽습니다.

## 8. 확장 포인트

- 새로운 기능 모듈은 `src/<Feature>` 아래에 네임스페이스와 동일한 폴더 구조로 추가합니다.
- `JobOptions`를 확장해 해시 검증, 리트라이 정책 등 추가 동작을 구현할 수 있습니다.

