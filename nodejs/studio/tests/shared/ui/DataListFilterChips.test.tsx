import { describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, within } from "@testing-library/react";
import { DataList, type DataListColumn } from "@/shared/ui/DataList";
import { FilterChips } from "@/shared/ui/FilterChips";

type Row = { id: string; name: string; owner: string };

const rows: Row[] = [
  { id: "r1", name: "alpha", owner: "sato" },
  { id: "r2", name: "beta", owner: "suzuki" },
];

const columns: DataListColumn<Row>[] = [
  { id: "name", header: "名前", width: "minmax(0,1fr)", hideLabelOnMobile: true, cell: (row) => row.name },
  { id: "owner", header: "担当", width: "8rem", cell: (row) => row.owner },
];

describe("DataList", () => {
  it("行ごとに列の値を出し、一覧の名前を付ける", () => {
    // Arrange / Act
    render(<DataList ariaLabel="定義" columns={columns} items={rows} getRowKey={(row) => row.id} />);

    // Assert
    const items = within(screen.getByRole("list", { name: "定義" })).getAllByRole("listitem");
    expect(items).toHaveLength(2);
    expect(within(items[0]).getByText("alpha")).toBeInTheDocument();
    expect(within(items[1]).getByText("suzuki")).toBeInTheDocument();
  });

  it("狭い幅の見出しは hideLabelOnMobile の列には出さない", () => {
    // Arrange / Act
    render(<DataList ariaLabel="定義" columns={columns} items={[rows[0]]} getRowKey={(row) => row.id} />);

    // Assert
    const item = screen.getByRole("listitem");
    expect(within(item).getByText("担当")).toBeInTheDocument();
    expect(within(item).queryByText("名前")).toBeNull();
  });

  it("renderRowFooter が返した要素を行の下に出す", () => {
    // Arrange / Act
    render(
      <DataList
        ariaLabel="定義"
        columns={columns}
        items={rows}
        getRowKey={(row) => row.id}
        renderRowFooter={(row) => (row.id === "r2" ? <p>失敗しました</p> : null)}
      />
    );

    // Assert
    const items = screen.getAllByRole("listitem");
    expect(within(items[1]).getByText("失敗しました")).toBeInTheDocument();
    expect(within(items[0]).queryByText("失敗しました")).toBeNull();
  });
});

describe("FilterChips", () => {
  it("条件が無いときは何も描かない", () => {
    // Arrange / Act
    const { container } = render(<FilterChips chips={[]} onClearAll={vi.fn()} />);

    // Assert
    expect(container.firstChild).toBeNull();
  });

  it("チップの × でその条件だけを外す", () => {
    // Arrange
    const onRemove = vi.fn();
    render(<FilterChips chips={[{ id: "name", label: "名前: order", onRemove }]} onClearAll={vi.fn()} />);

    // Act
    fireEvent.click(screen.getByRole("button", { name: "名前: order を外す" }));

    // Assert
    expect(onRemove).toHaveBeenCalledTimes(1);
  });

  it("条件が 1 つのときは「すべて外す」を出さない", () => {
    // Arrange / Act
    render(<FilterChips chips={[{ id: "name", label: "名前: order", onRemove: vi.fn() }]} onClearAll={vi.fn()} />);

    // Assert
    expect(screen.queryByRole("button", { name: "すべて外す" })).toBeNull();
  });

  it("条件が 2 つ以上のときは「すべて外す」で全部を外す", () => {
    // Arrange
    const onClearAll = vi.fn();
    render(
      <FilterChips
        chips={[
          { id: "name", label: "名前: order", onRemove: vi.fn() },
          { id: "deleted", label: "削除済みを含む", onRemove: vi.fn() },
        ]}
        onClearAll={onClearAll}
      />
    );

    // Act
    fireEvent.click(screen.getByRole("button", { name: "すべて外す" }));

    // Assert
    expect(onClearAll).toHaveBeenCalledTimes(1);
  });
});
