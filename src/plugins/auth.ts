import fp from 'fastify-plugin';
import jwt, { JwtPayload } from 'jsonwebtoken';
import { env } from '../config.js';
import type { AuthUser } from '../types.js';

const PUBLIC_ROUTES = new Set(['/healthz', '/readyz', '/auth/guest']);

function extractToken(header?: string) {
  if (!header) return null;
  const [scheme, token] = header.split(' ');
  if (scheme?.toLowerCase() !== 'bearer' || !token) return null;
  return token;
}

export const authPlugin = fp(async (fastify) => {
  const verifyToken = (token: string): AuthUser => {
    const decoded = jwt.verify(token, env.JWT_SECRET) as JwtPayload;
    const sub = decoded.sub;
    if (typeof sub !== 'string' || sub.length === 0) {
      throw new Error('invalid_sub');
    }
    return { id: sub, externalId: typeof decoded.externalId === 'string' ? decoded.externalId : undefined };
  };

  fastify.addHook('onRoute', (routeOptions) => {
    const publicRoute = routeOptions.url && PUBLIC_ROUTES.has(routeOptions.url);
    if (publicRoute) {
      return;
    }

    const guard = async (request: any, reply: any) => {
      const token = extractToken(request.headers.authorization);
      if (!token) {
        reply.status(401).send({ error: 'unauthorized' });
        return;
      }

      try {
        request.user = verifyToken(token);
      } catch (err) {
        reply.status(401).send({ error: 'unauthorized' });
        return;
      }
    };

    const existing = routeOptions.preHandler;
    if (Array.isArray(existing)) {
      routeOptions.preHandler = [guard, ...existing];
    } else if (existing) {
      routeOptions.preHandler = [guard, existing];
    } else {
      routeOptions.preHandler = [guard];
    }
  });
});


