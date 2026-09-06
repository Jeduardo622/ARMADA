export function verifyPrHead(
  args: string[],
  run?: (command: string, args: string[], options: object) => {
    status: number | null;
    stdout?: string;
    error?: Error;
  }
): string;
