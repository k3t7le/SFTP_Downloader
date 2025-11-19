# 사용 방법

이 문서는 SFTP Downloader를 직접 실행하는 운영자를 위해 작성되었습니다. 아래 순서를 차근차근 따라 하면 안전하게 파일을 받을 수 있습니다.

## 1. 사전 준비

- SFTP 서버에 접속 가능한 네트워크 경로와 계정 정보를 확보합니다. (아이디/비밀번호 또는 키 파일)
- `appsettings.json`에 적힌 로컬 폴더와 로그 폴더에 쓸 수 있는 권한 및 충분한 디스크 용량을 확보합니다.

## 2. `appsettings.json` 설정

솔루션 루트에 있는 `appsettings.json` 파일을 열고 다음 정보를 채웁니다.

```jsonc
{
  "Sftp": {
    "Host": "192.168.10.79",
    "Port": 2222,
    "Username": "acetech",
    "Password": "acetech"
  },
  "Logging": {
    "LogFolder": "D:/TMP_DATA/LOGS",
    "RetentionDays": 30
  },
  "Jobs": [
    {
      "Name": "AGING",
      "RemoteFolders": [ "/AGING", "/AGING_TMP" ],
      "LocalTargetFolder": "D:/TMP_DATA/AGING",
      "SearchPattern": "*.*",
      "DeleteRemoteAfterSuccess": true,
      "ArchiveFolder": "D:/TMP_DATA/AGING_ARC"
    }
  ]
}
```

- `Sftp`: 호스트, 포트, 사용자, 암호(또는 키 경로)
- `Logging`: 로그 저장 폴더 및 보존일
- `Jobs`: 잡 이름, 원격 폴더 목록, 로컬 저장 폴더, 검색 패턴, 원격 삭제 여부, (필요시) 아카이브 폴더

각 잡은 고유한 `Name`과 `LocalTargetFolder`를 가져야 하며, 여러 `RemoteFolders`를 순차적으로 처리합니다.

## 3. 최초 1회 빌드

```bash
dotnet restore
dotnet build SFTP_Downloader.sln
```

## 4. 실행

```bash
dotnet run --project SFTP_Downloader.csproj -- --job AGING
```

- `--job` 옵션을 생략하면 모든 잡을 실행합니다.
- 여러 잡을 지정하려면 `--job AGING --job CONFIRM` 또는 `--jobs=AGING,CONFIRM`처럼 입력합니다.
- 실행 중 `Ctrl+C`를 누르면 현재 작업을 안전하게 중단하고 요약 결과를 출력합니다.

## 5. 진행 상황 확인

- 콘솔에는 각 폴더당 최대 10회 정도만 진행률이 표시됩니다. 예) `/CONFIRM progress 381/5943 (~6%) | Success 381 | Fail 0`
- 상세 단계(다운로드, 검증, 원격 삭제)는 `Logging.LogFolder` 아래의 로그 파일에 기록됩니다.
- 실패한 파일은 콘솔 요약과 로그 파일 모두에 남으므로 파일명을 확인하고 조치할 수 있습니다.

## 6. 실행 후 점검

1. 프로그램이 출력한 ASCII 요약 배너를 확인합니다. 잡/폴더별 전체/성공/실패 건수와 실패 파일명이 정리되어 있습니다.
2. 각 `LocalTargetFolder`(및 선택적으로 `ArchiveFolder`)에 파일이 올바르게 도착했는지 확인합니다.
3. `.part` 임시 파일이 오래 남아 있지 않은지 점검합니다. 계속 남아 있다면 중간에 프로그램이 중단된 것이므로 재실행합니다.

이상의 단계를 통해 안전하게 SFTP 서버의 파일을 수집할 수 있습니다.
