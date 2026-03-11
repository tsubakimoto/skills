#!/usr/bin/env python3
"""
Backlog API v2 ユーティリティスクリプト

Usage:
    python backlog_api.py --space SPACE_ID --api-key API_KEY <command> [options]

Commands:
    get-space               スペース情報を取得
    get-projects            プロジェクト一覧を取得
    get-issues              課題一覧を取得
    get-issue ISSUE_KEY     課題を取得
    add-issue               課題を登録
    update-issue ISSUE_KEY  課題を更新
    add-comment ISSUE_KEY   コメントを追加
    get-users               ユーザー一覧を取得

Examples:
    python backlog_api.py --space myspace --api-key YOUR_KEY get-space
    python backlog_api.py --space myspace --api-key YOUR_KEY get-issues --project-id 12345
    python backlog_api.py --space myspace --api-key YOUR_KEY get-issue PROJECT-123
    python backlog_api.py --space myspace --api-key YOUR_KEY add-issue \\
        --project-id 12345 --summary "バグ修正" --issue-type-id 1 --priority-id 2
    python backlog_api.py --space myspace --api-key YOUR_KEY update-issue PROJECT-123 \\
        --status-id 2 --comment "対応中"
"""

import argparse
import json
import sys
from typing import Any

import requests


class BacklogClient:
    def __init__(self, space: str, api_key: str, tld: str = "com"):
        self.base_url = f"https://{space}.backlog.{tld}/api/v2"
        self.api_key = api_key

    def _request(self, method: str, path: str, params: dict | None = None, data: dict | None = None) -> Any:
        url = f"{self.base_url}{path}"
        q = {"apiKey": self.api_key}
        if params:
            q.update(params)
        resp = requests.request(method, url, params=q, data=data, timeout=30)
        resp.raise_for_status()
        return resp.json()

    # --- Space ---
    def get_space(self) -> dict:
        return self._request("GET", "/space")

    # --- Users ---
    def get_users(self) -> list[dict]:
        return self._request("GET", "/users")

    def get_myself(self) -> dict:
        return self._request("GET", "/users/myself")

    # --- Projects ---
    def get_projects(self) -> list[dict]:
        return self._request("GET", "/projects")

    def get_project(self, project_id_or_key: str) -> dict:
        return self._request("GET", f"/projects/{project_id_or_key}")

    # --- Issues ---
    def get_issues(
        self,
        project_id: int | None = None,
        status_id: list[int] | None = None,
        assignee_id: list[int] | None = None,
        keyword: str | None = None,
        count: int = 20,
        offset: int = 0,
        order: str = "desc",
    ) -> list[dict]:
        params: dict[str, Any] = {"count": count, "offset": offset, "order": order}
        if project_id is not None:
            params["projectId[]"] = project_id
        if status_id:
            params["statusId[]"] = status_id
        if assignee_id:
            params["assigneeId[]"] = assignee_id
        if keyword:
            params["keyword"] = keyword
        return self._request("GET", "/issues", params=params)

    def get_issue(self, issue_id_or_key: str) -> dict:
        return self._request("GET", f"/issues/{issue_id_or_key}")

    def add_issue(
        self,
        project_id: int,
        summary: str,
        issue_type_id: int,
        priority_id: int,
        description: str | None = None,
        assignee_id: int | None = None,
        due_date: str | None = None,
    ) -> dict:
        data: dict[str, Any] = {
            "projectId": project_id,
            "summary": summary,
            "issueTypeId": issue_type_id,
            "priorityId": priority_id,
        }
        if description:
            data["description"] = description
        if assignee_id:
            data["assigneeId"] = assignee_id
        if due_date:
            data["dueDate"] = due_date
        return self._request("POST", "/issues", data=data)

    def update_issue(self, issue_id_or_key: str, **fields: Any) -> dict:
        return self._request("PATCH", f"/issues/{issue_id_or_key}", data=fields)

    # --- Comments ---
    def get_comments(self, issue_id_or_key: str, count: int = 20) -> list[dict]:
        return self._request("GET", f"/issues/{issue_id_or_key}/comments", params={"count": count})

    def add_comment(self, issue_id_or_key: str, content: str) -> dict:
        return self._request("POST", f"/issues/{issue_id_or_key}/comments", data={"content": content})

    # --- Master data ---
    def get_priorities(self) -> list[dict]:
        return self._request("GET", "/priorities")

    def get_statuses(self, project_id_or_key: str) -> list[dict]:
        return self._request("GET", f"/projects/{project_id_or_key}/statuses")

    def get_issue_types(self, project_id_or_key: str) -> list[dict]:
        return self._request("GET", f"/projects/{project_id_or_key}/issueTypes")


def main() -> None:
    parser = argparse.ArgumentParser(description="Backlog API v2 CLI")
    parser.add_argument("--space", required=True, help="Backlogスペース名")
    parser.add_argument("--api-key", required=True, help="APIキー")
    parser.add_argument("--tld", default="com", choices=["com", "jp"], help="TLD (デフォルト: com)")

    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("get-space", help="スペース情報を取得")
    sub.add_parser("get-users", help="ユーザー一覧を取得")
    sub.add_parser("get-projects", help="プロジェクト一覧を取得")

    p_issues = sub.add_parser("get-issues", help="課題一覧を取得")
    p_issues.add_argument("--project-id", type=int)
    p_issues.add_argument("--status-id", type=int, nargs="+")
    p_issues.add_argument("--assignee-id", type=int, nargs="+")
    p_issues.add_argument("--keyword")
    p_issues.add_argument("--count", type=int, default=20)
    p_issues.add_argument("--offset", type=int, default=0)
    p_issues.add_argument("--order", default="desc", choices=["asc", "desc"])

    p_issue = sub.add_parser("get-issue", help="課題を取得")
    p_issue.add_argument("issue_key", help="課題キーまたはID (例: PROJECT-123)")

    p_add = sub.add_parser("add-issue", help="課題を登録")
    p_add.add_argument("--project-id", type=int, required=True)
    p_add.add_argument("--summary", required=True)
    p_add.add_argument("--issue-type-id", type=int, required=True)
    p_add.add_argument("--priority-id", type=int, required=True)
    p_add.add_argument("--description")
    p_add.add_argument("--assignee-id", type=int)
    p_add.add_argument("--due-date", help="yyyy-MM-dd")

    p_update = sub.add_parser("update-issue", help="課題を更新")
    p_update.add_argument("issue_key", help="課題キーまたはID")
    p_update.add_argument("--summary")
    p_update.add_argument("--status-id", type=int)
    p_update.add_argument("--assignee-id", type=int)
    p_update.add_argument("--priority-id", type=int)
    p_update.add_argument("--comment")

    p_comment = sub.add_parser("add-comment", help="コメントを追加")
    p_comment.add_argument("issue_key", help="課題キーまたはID")
    p_comment.add_argument("--content", required=True)

    args = parser.parse_args()
    client = BacklogClient(args.space, args.api_key, args.tld)

    try:
        if args.command == "get-space":
            result = client.get_space()
        elif args.command == "get-users":
            result = client.get_users()
        elif args.command == "get-projects":
            result = client.get_projects()
        elif args.command == "get-issues":
            result = client.get_issues(
                project_id=args.project_id,
                status_id=args.status_id,
                assignee_id=args.assignee_id,
                keyword=args.keyword,
                count=args.count,
                offset=args.offset,
                order=args.order,
            )
        elif args.command == "get-issue":
            result = client.get_issue(args.issue_key)
        elif args.command == "add-issue":
            result = client.add_issue(
                project_id=args.project_id,
                summary=args.summary,
                issue_type_id=args.issue_type_id,
                priority_id=args.priority_id,
                description=args.description,
                assignee_id=args.assignee_id,
                due_date=args.due_date,
            )
        elif args.command == "update-issue":
            fields: dict[str, Any] = {}
            if args.summary:
                fields["summary"] = args.summary
            if args.status_id:
                fields["statusId"] = args.status_id
            if args.assignee_id:
                fields["assigneeId"] = args.assignee_id
            if args.priority_id:
                fields["priorityId"] = args.priority_id
            if args.comment:
                fields["comment"] = args.comment
            result = client.update_issue(args.issue_key, **fields)
        elif args.command == "add-comment":
            result = client.add_comment(args.issue_key, args.content)
        else:
            parser.print_help()
            sys.exit(1)

        print(json.dumps(result, ensure_ascii=False, indent=2))

    except requests.HTTPError as e:
        print(f"HTTPエラー: {e.response.status_code} {e.response.text}", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
