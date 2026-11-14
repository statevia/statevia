import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SelectField } from "@/shared/ui/SelectField";
import { TextAreaField } from "@/shared/ui/TextAreaField";
import { TextField } from "@/shared/ui/TextField";

describe("入力欄の部品", () => {
  it("required のとき、項目名の右に「*」を付け、入力に required を渡す", () => {
    // Arrange / Act
    render(<TextField label="名前" required />);

    // Assert
    const input = screen.getByRole("textbox", { name: "名前" });
    expect(input).toBeRequired();
    expect(screen.getByText("名前")).toHaveClass("after:content-['*']");
  });

  it("説明は aria-describedby で入力と結ぶ", () => {
    // Arrange / Act
    render(<TextField label="名前" hint="64 文字まで" />);

    // Assert
    expect(screen.getByRole("textbox", { name: "名前" })).toHaveAccessibleDescription("64 文字まで");
  });

  it("エラーのときは説明の代わりにエラーを出し、aria-invalid と赤い枠にする", () => {
    // Arrange / Act
    render(<TextField label="cron 式" hint="5 項目" error="5 つの項目が必要です" />);

    // Assert
    const input = screen.getByRole("textbox", { name: "cron 式" });
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveClass("border-md-error");
    expect(input).toHaveAccessibleDescription("5 つの項目が必要です");
    expect(screen.queryByText("5 項目")).toBeNull();
  });

  it("SelectField は選択肢を描き、ラベルで選べる", () => {
    // Arrange / Act
    render(
      <SelectField label="タイムゾーン" defaultValue="Asia/Tokyo">
        <option value="UTC">UTC</option>
        <option value="Asia/Tokyo">Asia/Tokyo</option>
      </SelectField>
    );

    // Assert
    expect(screen.getByRole("combobox", { name: "タイムゾーン" })).toHaveValue("Asia/Tokyo");
  });

  it("TextAreaField は複数行の入力として描く", () => {
    // Arrange / Act
    render(<TextAreaField label="説明" defaultValue="平日の朝" />);

    // Assert
    expect(screen.getByRole("textbox", { name: "説明" }).tagName).toBe("TEXTAREA");
  });
});
