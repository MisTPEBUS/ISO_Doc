import {
  FileCheck2,
  FileText,
  Folder,
  History,
  Paperclip,
  Search,
  ShieldCheck,
  type LucideIcon,
} from "lucide-react";
import { useLayoutEffect, useRef, useState } from "react";

// Duotone crop of Unsplash RbFDzMKTH6Q (Unsplash License); see public/images/CREDITS.md.
const PHOTO = `${import.meta.env.BASE_URL}images/login-hero-duotone.webp`;
const PHOTO_CLIP_ID = "login1-hero-photo";

interface Point {
  x: number;
  y: number;
}

interface Size {
  width: number;
  height: number;
}

const clamp = (value: number, min: number, max: number) =>
  Math.min(max, Math.max(min, value));
const fmt = (value: number) => value.toFixed(1);

/*
 * A photo chevron (left) and a panel chevron (right, same colour as the form
 * panel) point at each other, with a check and a caret between them forming an
 * X. The shapes are computed in pixels from the measured box, so the desktop
 * column, the narrow tablet column and the short mobile strip keep the same
 * motif without stretching:
 * - the panel chevron always covers the full right edge and meets the form;
 * - each band runs parallel to its neighbouring chevron edge.
 */
interface HeroGeometry {
  photoPath: string;
  panelPath: string;
  checkPath: string;
  caretPath: string;
  stroke: number;
  /** x of the photo chevron's tip, which is also the photo's width. */
  photoTip: number;
  slope: number;
  centerY: number;
}

// Keeps very wide mobile strips from flattening the X; the photo widens instead.
const MIN_SLOPE = 0.55;
// Share of the width the photo keeps along the top and bottom edges.
const PHOTO_EDGE_SHARE = 0.16;

function computeGeometry({ width, height }: Size): HeroGeometry {
  const centerY = height / 2;
  const stroke = clamp(height * 0.024, 5, 15);
  const tipGap = clamp(width * 0.085, 20, 44);
  // The panel edge must reach the right side 4px past the top and bottom.
  const reach = 2 * centerY + 4;
  const slope = Math.max(
    reach / (width * (1 - PHOTO_EDGE_SHARE) - tipGap),
    MIN_SLOPE,
  );
  const photoTip = width - tipGap - reach / slope + centerY / slope;
  const panelTip = photoTip + tipGap;
  const centerX = photoTip + tipGap / 2;

  const length = Math.hypot(1, slope);
  const ux = 1 / length;
  const uy = slope / length;
  const overshoot = stroke;

  const radius = clamp(height * 0.025, 5, 16);
  const rx = radius * ux;
  const ry = radius * uy;
  const photoEdge = photoTip - (centerY + overshoot) / slope;
  const panelEdge = panelTip + (centerY + overshoot) / slope;
  const right = Math.max(panelEdge, width) + overshoot;

  const photoPath =
    `M${-overshoot} ${-overshoot}H${fmt(photoEdge)}` +
    `L${fmt(photoTip - rx)} ${fmt(centerY - ry)}` +
    `Q${fmt(photoTip)} ${fmt(centerY)} ${fmt(photoTip - rx)} ${fmt(centerY + ry)}` +
    `L${fmt(photoEdge)} ${fmt(height + overshoot)}H${-overshoot}Z`;
  const panelPath =
    `M${fmt(panelEdge)} ${-overshoot}` +
    `L${fmt(panelTip + rx)} ${fmt(centerY - ry)}` +
    `Q${fmt(panelTip)} ${fmt(centerY)} ${fmt(panelTip + rx)} ${fmt(centerY + ry)}` +
    `L${fmt(panelEdge)} ${fmt(height + overshoot)}H${fmt(right)}V${-overshoot}Z`;

  // The band tips sit 1.65 strokes above and below the centre line.
  const armGap = 1.65 * stroke;
  const arm = clamp(height * 0.11, 16, 70);
  const check: Point = { x: centerX, y: centerY - armGap };
  const caret: Point = { x: centerX, y: centerY + armGap };
  const checkPath =
    `M${fmt(check.x - arm * ux)} ${fmt(check.y - arm * uy)}` +
    `L${fmt(check.x)} ${fmt(check.y)}` +
    `L${fmt(check.x + (check.y + overshoot) / slope)} ${fmt(-overshoot)}`;
  const caretPath =
    `M${fmt(caret.x - (height + overshoot - caret.y) / slope)} ${fmt(height + overshoot)}` +
    `L${fmt(caret.x)} ${fmt(caret.y)}` +
    `L${fmt(caret.x + arm * ux)} ${fmt(caret.y + arm * uy)}`;

  return {
    photoPath,
    panelPath,
    checkPath,
    caretPath,
    stroke,
    photoTip,
    slope,
    centerY,
  };
}

// Network positions are fractions of the photo width (u) and height (v).
interface Placement {
  u: number;
  v: number;
}

const HUB: Placement = { u: 0.64, v: 0.41 };
// The mobile strip is too short for the network; it shows photo and X only.
const MIN_NETWORK_HEIGHT = 360;
const HUB_RING = 32;
const HUB_CORE = 24;
const NODE_RADIUS = 17;
const DOT_RADIUS = 4;

const ICON_NODES = {
  folder: { u: 0.25, v: 0.23, icon: Folder },
  permission: { u: 0.52, v: 0.285, icon: ShieldCheck },
  history: { u: 0.19, v: 0.41, icon: History },
  document: { u: 0.81, v: 0.53, icon: FileText },
  attachment: { u: 0.23, v: 0.62, icon: Paperclip },
  search: { u: 0.52, v: 0.675, icon: Search },
} satisfies Record<string, Placement & { icon: LucideIcon }>;

const DOT_NODES = {
  top: { u: 0.42, v: 0.15 },
  topFar: { u: 0.3, v: 0.08 },
  left: { u: 0.09, v: 0.525 },
  bottomLeft: { u: 0.14, v: 0.74 },
  bottom: { u: 0.41, v: 0.815 },
  bottomRight: { u: 0.59, v: 0.76 },
} satisfies Record<string, Placement>;

type IconNodeId = keyof typeof ICON_NODES;
type DotNodeId = keyof typeof DOT_NODES;
type NodeId = "hub" | IconNodeId | DotNodeId;

const LINKS: ReadonlyArray<readonly [NodeId, NodeId]> = [
  ["hub", "permission"],
  ["hub", "history"],
  ["hub", "document"],
  ["hub", "search"],
  ["permission", "folder"],
  ["folder", "history"],
  ["folder", "top"],
  ["folder", "topFar"],
  ["history", "left"],
  ["history", "attachment"],
  ["attachment", "search"],
  ["attachment", "bottomLeft"],
  ["search", "bottom"],
  ["search", "bottomRight"],
  ["document", "search"],
];

interface NetworkLayout {
  hub: Point;
  icons: Array<Point & { id: IconNodeId; icon: LucideIcon }>;
  dots: Array<Point & { id: DotNodeId }>;
  links: Array<{ id: string; from: Point; to: Point }>;
}

// Only nodes that fit inside the photo chevron are drawn; without room for
// the hub (narrow tablet column, mobile strip) the network is left out.
function layoutNetwork(
  geometry: HeroGeometry,
  height: number,
): NetworkLayout | null {
  if (height < MIN_NETWORK_HEIGHT) {
    return null;
  }

  const place = ({ u, v }: Placement): Point => ({
    x: u * geometry.photoTip,
    y: v * height,
  });
  const fits = ({ x, y }: Point, radius: number) => {
    const edge =
      geometry.photoTip - Math.abs(y - geometry.centerY) / geometry.slope;
    return (
      x - radius >= 4 &&
      y - radius >= 4 &&
      y + radius <= height - 4 &&
      x + radius + 6 <= edge
    );
  };

  const hub = place(HUB);
  if (!fits(hub, HUB_RING)) {
    return null;
  }

  const visible = new Map<NodeId, Point>([["hub", hub]]);
  const icons: NetworkLayout["icons"] = [];
  for (const id of Object.keys(ICON_NODES) as IconNodeId[]) {
    const node = ICON_NODES[id];
    const point = place(node);
    if (fits(point, NODE_RADIUS)) {
      visible.set(id, point);
      icons.push({ id, icon: node.icon, ...point });
    }
  }
  const dots: NetworkLayout["dots"] = [];
  for (const id of Object.keys(DOT_NODES) as DotNodeId[]) {
    const point = place(DOT_NODES[id]);
    if (fits(point, DOT_RADIUS)) {
      visible.set(id, point);
      dots.push({ id, ...point });
    }
  }
  const links = LINKS.flatMap(([fromId, toId]) => {
    const from = visible.get(fromId);
    const to = visible.get(toId);
    return from && to ? [{ id: `${fromId}-${toId}`, from, to }] : [];
  });

  return { hub, icons, dots, links };
}

function useElementSize<T extends Element>() {
  const ref = useRef<T>(null);
  const [size, setSize] = useState<Size>({ width: 0, height: 0 });

  useLayoutEffect(() => {
    const element = ref.current;
    if (!element) {
      return undefined;
    }

    const update = () => {
      const { width, height } = element.getBoundingClientRect();
      setSize((current) =>
        current.width === width && current.height === height
          ? current
          : { width, height },
      );
    };
    update();
    const observer = new ResizeObserver(update);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return [ref, size] as const;
}

export function LoginHeroArt() {
  const [ref, size] = useElementSize<HTMLDivElement>();
  const geometry =
    size.width > 0 && size.height > 0 ? computeGeometry(size) : null;
  const network = geometry ? layoutNetwork(geometry, size.height) : null;

  return (
    <div ref={ref} className="absolute inset-0">
      {geometry && (
        <svg
          className="block size-full"
          viewBox={`0 0 ${fmt(size.width)} ${fmt(size.height)}`}
          aria-hidden="true"
          focusable="false"
        >
          <defs>
            <clipPath id={PHOTO_CLIP_ID}>
              <path d={geometry.photoPath} />
            </clipPath>
          </defs>

          <rect width={size.width} height={size.height} fill="#FFFFFF" />
          <path d={geometry.panelPath} fill="#EEF4F8" />

          <g clipPath={`url(#${PHOTO_CLIP_ID})`}>
            <image
              href={PHOTO}
              width={geometry.photoTip}
              height={size.height}
              preserveAspectRatio="xMidYMid slice"
            />

            {/* Document network over the photo, like the reference's node graph. */}
            {network && (
              <>
                <g stroke="#CFE3F8" strokeWidth="1.6">
                  {network.links.map(({ id, from, to }) => (
                    <line
                      key={id}
                      x1={from.x}
                      y1={from.y}
                      x2={to.x}
                      y2={to.y}
                    />
                  ))}
                </g>
                <g fill="#CFE3F8">
                  {network.dots.map(({ id, x, y }) => (
                    <circle key={id} cx={x} cy={y} r={DOT_RADIUS} />
                  ))}
                </g>
                {network.icons.map(({ id, x, y, icon: Icon }) => (
                  <g key={id}>
                    <circle
                      cx={x}
                      cy={y}
                      r={NODE_RADIUS}
                      fill="#1D56A3"
                      stroke="#FFFFFF"
                      strokeWidth="2"
                    />
                    <Icon
                      x={x - 8}
                      y={y - 8}
                      size={16}
                      color="#FFFFFF"
                      strokeWidth={2}
                    />
                  </g>
                ))}
                <circle
                  cx={network.hub.x}
                  cy={network.hub.y}
                  r={HUB_RING}
                  fill="none"
                  stroke="#FFFFFF"
                  strokeWidth="2"
                />
                <circle
                  cx={network.hub.x}
                  cy={network.hub.y}
                  r={HUB_CORE}
                  fill="#FFFFFF"
                />
                <FileCheck2
                  x={network.hub.x - 13}
                  y={network.hub.y - 13}
                  size={26}
                  color="#153A5B"
                  strokeWidth={2}
                />
              </>
            )}
          </g>

          <g
            fill="none"
            strokeWidth={geometry.stroke}
            strokeLinecap="round"
            strokeLinejoin="round"
          >
            <path d={geometry.checkPath} stroke="#153A5B" />
            <path d={geometry.caretPath} stroke="#60A5FA" />
          </g>
        </svg>
      )}
    </div>
  );
}
