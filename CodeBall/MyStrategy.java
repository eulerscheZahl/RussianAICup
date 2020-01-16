import java.io.FileNotFoundException;
import java.io.PrintWriter;
import java.util.ArrayList;

import model.Action;
import model.Arena;
import model.Ball;
import model.Game;
import model.Robot;
import model.Rules;

public final class MyStrategy implements Strategy {
	private double deltaTime = 0.1;

	class Vector {
		public double x;
		public double y;
		public double z;

		public Vector(double x, double y, double z) {
			this.x = x;
			this.y = y;
			this.z = z;
		}

		public double dist(Vector p) {
			double dx = x - p.x;
			double dy = y - p.y;
			double dz = z - p.z;
			return Math.sqrt(dx * dx + dy * dy + dz * dz);
		}

		public Vector add(Vector v2) {
			return new Vector(x + v2.x, y + v2.y, z + v2.z);
		}

		public Vector sub(Vector v2) {
			return new Vector(x - v2.x, y - v2.y, z - v2.z);
		}

		public Vector to(Vector target) {
			return new Vector(target.x - x, target.y - y, target.z - z);
		}

		public Vector clamp(double maxLength) {
			double l = length();
			if (l <= maxLength)
				return new Vector(x, y, z);
			double d = maxLength / l;
			return new Vector(x * d, y * d, z * d);
		}

		public double length() {
			return dist(new Vector(0, 0, 0));
		}

		public void scaleTo(double targetLength) {
			double length = length();
			double factor = targetLength / length;
			x *= factor;
			y *= factor;
			z *= factor;
		}

		public Vector normalize() {
			double l = length();
			return new Vector(x / l, y / l, z / l);
		}

		public double dot(Vector v2) {
			return x * v2.x + y * v2.y + z * v2.z;
		}

		public Vector cross(Vector v2) {
			return new Vector(y * v2.z - z * v2.y, z * v2.x - x * v2.z, x * v2.y + y * v2.x);
		}

		public Vector mul(double d) {
			return new Vector(x * d, y * d, z * d);
		}

		@Override
		public String toString() {
			return "(" + x + "/" + y + "/" + z + ")";
		}
	}

	class Entity {
		public Vector position;
		public Vector velocity;
		public double radiusChangeSpeed = 0;
		public double mass;
		public double radius;
		public double arenaE;
		public double nitro;

		public Entity(Robot robot, Rules rules) {
			this.position = new Vector(robot.x, robot.y, robot.z);
			this.velocity = new Vector(robot.velocity_x, robot.velocity_y, robot.velocity_z);
			this.mass = rules.ROBOT_MASS;
			radius = rules.ROBOT_MIN_RADIUS;
			arenaE = rules.ROBOT_ARENA_E;
			nitro = robot.nitro_amount;
		}

		public Entity(Ball ball, Rules rules) {
			this.position = new Vector(ball.x, ball.y, ball.z);
			this.velocity = new Vector(ball.velocity_x, ball.velocity_y, ball.velocity_z);
			this.mass = rules.BALL_MASS;
			radius = rules.BALL_RADIUS;
			arenaE = rules.BALL_ARENA_E;
		}

		public Entity(Entity entity) {
			this.position = new Vector(entity.position.x, entity.position.y, entity.position.z);
			this.velocity = new Vector(entity.velocity.x, entity.velocity.y, entity.velocity.z);
			this.mass = entity.mass;
			this.radius = entity.radius;
			this.arenaE = entity.arenaE;
			this.nitro = entity.nitro;
		}
	}

	class Normal {
		public double dist;
		public Vector normal;

		public Normal(double dist, Vector normal) {
			this.dist = dist;
			this.normal = normal;
		}
	}

	public void collideEntities(Entity a, Entity b, Rules rules) {
		Vector delta_position = b.position.sub(a.position);
		double distance = delta_position.length();
		double penetration = a.radius + b.radius - distance;
		if (penetration > 0) {
			double k_a = (1 / a.mass) / ((1 / a.mass) + (1 / b.mass));
			double k_b = (1 / b.mass) / ((1 / a.mass) + (1 / b.mass));
			Vector normal = delta_position.normalize();
			a.position = a.position.sub(normal.mul(penetration * k_a));
			b.position = b.position.add(normal.mul(penetration * k_b));
			double delta_velocity = b.velocity.sub(a.velocity).dot(normal) - b.radiusChangeSpeed - a.radiusChangeSpeed;
			if (delta_velocity < 0) {
				Vector impulse = normal.mul((1 + (rules.MIN_HIT_E + rules.MAX_HIT_E) / 2) * delta_velocity);
				a.velocity = a.velocity.add(impulse.mul(k_a));
				b.velocity = b.velocity.sub(impulse.mul(k_b));
			}
		}
	}

	public void update(Arena arena, Rules rules, Entity[] robots, Entity ball, Action[] plans) {
		int[] ticks = new int[] { rules.MICROTICKS_PER_TICK };
		if (robots.length > 0) {
			ticks = new int[21];
			ticks[0] = 1;
			ticks[1] = rules.MICROTICKS_PER_TICK / 20 - 1;
			for (int i = 2; i < ticks.length; i++)
				ticks[i] = rules.MICROTICKS_PER_TICK / 20;
		}
		for (int tickCount : ticks) {
			deltaTime = (double) tickCount / (rules.MICROTICKS_PER_TICK * rules.TICKS_PER_SECOND);
			for (int i = 0; i < plans.length && i < robots.length; i++) {
				Entity robot = robots[i];
				Action action = plans[i];
				Normal normal = danToArena(robot.position, arena);
				boolean touch = normal.dist <= robot.radius;
				if (action == null)
					action = new Action();
				Vector targetVelocity = new Vector(action.target_velocity_x, action.target_velocity_y, action.target_velocity_z);
				if (touch) {
					Vector touchNormal = normal.normal;
					targetVelocity = targetVelocity.clamp(rules.ROBOT_MAX_GROUND_SPEED);
					targetVelocity = targetVelocity.sub(touchNormal.mul(touchNormal.dot(targetVelocity)));
					Vector target_velocity_change = targetVelocity.sub(robot.velocity);
					if (target_velocity_change.length() > 0) {
						double acceleration = rules.ROBOT_ACCELERATION * Math.max(0, touchNormal.y);
						robot.velocity = robot.velocity.add(target_velocity_change.normalize().mul(acceleration * deltaTime).clamp(target_velocity_change.length()));
					}
				}
				if (action.use_nitro) {
					Vector target_velocity_change = targetVelocity.sub(robot.velocity).clamp(robot.nitro * rules.NITRO_POINT_VELOCITY_CHANGE);
					if (target_velocity_change.length() > 0) {
						Vector acceleration = target_velocity_change.normalize().mul(rules.ROBOT_NITRO_ACCELERATION);
						Vector velocity_change = acceleration.mul(deltaTime).clamp(target_velocity_change.length());
						robot.velocity = robot.velocity.add(velocity_change);
						robot.nitro -= velocity_change.length() / rules.NITRO_POINT_VELOCITY_CHANGE;
					}
				}
				move(robot, rules);
				robot.radius = rules.ROBOT_MIN_RADIUS + (rules.ROBOT_MAX_RADIUS - rules.ROBOT_MIN_RADIUS) * action.jump_speed / rules.ROBOT_MAX_JUMP_SPEED;
				robot.radiusChangeSpeed = action.jump_speed;
			}
			move(ball, rules);
			for (int i = 0; i < robots.length; i++) {
				for (int j = i + 1; j < robots.length; j++) {
					collideEntities(robots[i], robots[j], rules);
				}
			}
			for (int i = 0; i < robots.length; i++) {
				collideEntities(robots[i], ball, rules);
				collideWithArena(robots[i], arena);
			}
			collideWithArena(ball, arena);
		}
	}

	Vector collideWithArena(Entity e, Arena arena) {
		Normal normal = danToArena(e.position, arena);
		double penetration = e.radius - normal.dist;
		if (penetration > 0) {
			e.position = e.position.add(normal.normal.mul(penetration));
			double velocity = e.velocity.dot(normal.normal) - e.radiusChangeSpeed;
			if (velocity < 0) {
				e.velocity = e.velocity.sub(normal.normal.mul(velocity * (1 + e.arenaE)));
				return normal.normal;
			}
		}
		return null;
	}

	void move(Entity e, Rules rules) {
		e.velocity = e.velocity.clamp(rules.MAX_ENTITY_SPEED);
		e.position = e.position.add(e.velocity.mul(deltaTime));
		e.position.y -= rules.GRAVITY * deltaTime * deltaTime / 2;
		e.velocity.y -= rules.GRAVITY * deltaTime;
	}

	Normal dan_to_plane(Vector point, Vector point_on_plane, Vector plane_normal) {
		return new Normal(point.sub(point_on_plane).dot(plane_normal), plane_normal);
	}

	Normal dan_to_sphere_inner(Vector point, Vector sphere_center, double sphere_radius) {
		return new Normal(sphere_radius - point.sub(sphere_center).length(), sphere_center.sub(point).normalize());
	}

	Normal dan_to_sphere_outer(Vector point, Vector sphere_center, double sphere_radius) {
		return new Normal(point.sub(sphere_center).length() - sphere_radius, point.sub(sphere_center).normalize());
	}

	Normal min(Normal a, Normal b) {
		if (b.dist < a.dist)
			return b;
		return a;
	}

	double clamp(double v, double min, double max) {
		if (v < min)
			v = min;
		if (v > max)
			v = max;
		return v;
	}

	Normal dan_to_arena_quarter(Vector point, Arena arena) {
		// Ground
		Normal dan = dan_to_plane(point, new Vector(0, 0, 0), new Vector(0, 1, 0));
		// Ceiling
		dan = min(dan, dan_to_plane(point, new Vector(0, arena.height, 0), new Vector(0, -1, 0)));
		// Side x
		dan = min(dan, dan_to_plane(point, new Vector(arena.width / 2, 0, 0), new Vector(-1, 0, 0)));
		// Side z (goal)
		dan = min(dan, dan_to_plane(point, new Vector(0, 0, (arena.depth / 2) + arena.goal_depth), new Vector(0, 0, -1)));
		// Side z
		double vx = point.x - ((arena.goal_width / 2) - arena.goal_top_radius);
		double vy = point.y - (arena.goal_height - arena.goal_top_radius);
		if (point.x >= (arena.goal_width / 2) + arena.goal_side_radius || point.y >= arena.goal_height + arena.goal_side_radius || (vx > 0 && vy > 0 && Math.sqrt(vx * vx + vy * vy) >= arena.goal_top_radius + arena.goal_side_radius)) {
			dan = min(dan, dan_to_plane(point, new Vector(0, 0, arena.depth / 2), new Vector(0, 0, -1)));
		}
		// Side x & ceiling (goal)
		if (point.z >= (arena.depth / 2) + arena.goal_side_radius) {
			// x
			dan = min(dan, dan_to_plane(point, new Vector(arena.goal_width / 2, 0, 0), new Vector(-1, 0, 0)));
			// y
			dan = min(dan, dan_to_plane(point, new Vector(0, arena.goal_height, 0), new Vector(0, -1, 0)));
		}
		// Goal back corners
		if (point.z > (arena.depth / 2) + arena.goal_depth - arena.bottom_radius) {
			dan = min(dan, dan_to_sphere_inner(point, new Vector(clamp(point.x, arena.bottom_radius - (arena.goal_width / 2), (arena.goal_width / 2) - arena.bottom_radius), clamp(point.y, arena.bottom_radius, arena.goal_height - arena.goal_top_radius), (arena.depth / 2) + arena.goal_depth - arena.bottom_radius), arena.bottom_radius));
		}
		// Corner
		if (point.x > (arena.width / 2) - arena.corner_radius && point.z > (arena.depth / 2) - arena.corner_radius) {
			dan = min(dan, dan_to_sphere_inner(point, new Vector((arena.width / 2) - arena.corner_radius, point.y, (arena.depth / 2) - arena.corner_radius), arena.corner_radius));
		}
		// Goal outer corner
		if (point.z < (arena.depth / 2) + arena.goal_side_radius) {
			// Side x
			if (point.x < (arena.goal_width / 2) + arena.goal_side_radius)
				dan = min(dan, dan_to_sphere_outer(point, new Vector((arena.goal_width / 2) + arena.goal_side_radius, point.y, (arena.depth / 2) + arena.goal_side_radius), arena.goal_side_radius));
			// Ceiling
			if (point.y < arena.goal_height + arena.goal_side_radius)
				dan = min(dan, dan_to_sphere_outer(point, new Vector(point.x, arena.goal_height + arena.goal_side_radius, (arena.depth / 2) + arena.goal_side_radius), arena.goal_side_radius));
			// Top corner
			double ox = (arena.goal_width / 2) - arena.goal_top_radius;
			double oy = arena.goal_height - arena.goal_top_radius;
			vx = point.x - ox;
			vy = point.y - oy;
			if (vx > 0 && vy > 0) {
				double l = Math.sqrt(vx * vx + vy * vy);
				ox += vx / l * (arena.goal_top_radius + arena.goal_side_radius);
				oy += vy / l * (arena.goal_top_radius + arena.goal_side_radius);
				dan = min(dan, dan_to_sphere_outer(point, new Vector(ox, oy, (arena.depth / 2) + arena.goal_side_radius), arena.goal_side_radius));
			}
		}
		// Goal inside top corners
		if (point.z > (arena.depth / 2) + arena.goal_side_radius && point.y > arena.goal_height - arena.goal_top_radius) {
			// Side x
			if (point.x > (arena.goal_width / 2) - arena.goal_top_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector((arena.goal_width / 2) - arena.goal_top_radius, arena.goal_height - arena.goal_top_radius, point.z), arena.goal_top_radius));
			// Side z
			if (point.z > (arena.depth / 2) + arena.goal_depth - arena.goal_top_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector(point.x, arena.goal_height - arena.goal_top_radius, (arena.depth / 2) + arena.goal_depth - arena.goal_top_radius), arena.goal_top_radius));
		}
		// Bottom corners
		if (point.y < arena.bottom_radius) {
			// Side x
			if (point.x > (arena.width / 2) - arena.bottom_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector((arena.width / 2) - arena.bottom_radius, arena.bottom_radius, point.z), arena.bottom_radius));
			// Side z
			if (point.z > (arena.depth / 2) - arena.bottom_radius && point.x >= (arena.goal_width / 2) + arena.goal_side_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector(point.x, arena.bottom_radius, (arena.depth / 2) - arena.bottom_radius), arena.bottom_radius));
			// Side z (goal)
			if (point.z > (arena.depth / 2) + arena.goal_depth - arena.bottom_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector(point.x, arena.bottom_radius, (arena.depth / 2) + arena.goal_depth - arena.bottom_radius), arena.bottom_radius));
			// Goal outer corner
			double ox = (arena.goal_width / 2) + arena.goal_side_radius;
			double oy = (arena.depth / 2) + arena.goal_side_radius;
			vx = point.x - ox;
			vy = point.z - oy;
			if (vx < 0 && vy < 0 && Math.sqrt(vx * vx + vy * vy) < arena.goal_side_radius + arena.bottom_radius) {
				double l = Math.sqrt(vx * vx + vy * vy);
				ox += vx / l * (arena.goal_side_radius + arena.bottom_radius);
				oy += vy / l * (arena.goal_side_radius + arena.bottom_radius);
				dan = min(dan, dan_to_sphere_inner(point, new Vector(ox, arena.bottom_radius, oy), arena.bottom_radius));
			}
			// Side x (goal)
			if (point.z >= (arena.depth / 2) + arena.goal_side_radius && point.x > (arena.goal_width / 2) - arena.bottom_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector((arena.goal_width / 2) - arena.bottom_radius, arena.bottom_radius, point.z), arena.bottom_radius));
			// Corner
			if (point.x > (arena.width / 2) - arena.corner_radius && point.z > (arena.depth / 2) - arena.corner_radius) {
				double corner_ox = (arena.width / 2) - arena.corner_radius;
				double corner_oy = (arena.depth / 2) - arena.corner_radius;
				double nx = point.x - corner_ox;
				double ny = point.z - corner_oy;
				double dist = Math.sqrt(nx * nx + ny * ny);
				if (dist > arena.corner_radius - arena.bottom_radius) {
					nx /= dist;
					ny /= dist;
					double o2x = corner_ox + nx * (arena.corner_radius - arena.bottom_radius);
					double o2y = corner_oy + ny * (arena.corner_radius - arena.bottom_radius);
					dan = min(dan, dan_to_sphere_inner(point, new Vector(o2x, arena.bottom_radius, o2y), arena.bottom_radius));
				}
			}
		}
		// Ceiling corners
		if (point.y > arena.height - arena.top_radius) {
			// Side x
			if (point.x > (arena.width / 2) - arena.top_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector((arena.width / 2) - arena.top_radius, arena.height - arena.top_radius, point.z), arena.top_radius));
			// Side z
			if (point.z > (arena.depth / 2) - arena.top_radius)
				dan = min(dan, dan_to_sphere_inner(point, new Vector(point.x, arena.height - arena.top_radius, (arena.depth / 2) - arena.top_radius), arena.top_radius));
			// Corner
			if (point.x > (arena.width / 2) - arena.corner_radius && point.z > (arena.depth / 2) - arena.corner_radius) {
				double corner_ox = (arena.width / 2) - arena.corner_radius;
				double corner_oy = (arena.depth / 2) - arena.corner_radius;
				double dvx = point.x - corner_ox;
				double dvy = point.z - corner_oy;
				double len = Math.sqrt(dvx * dvx + dvy * dvy);
				if (len > arena.corner_radius - arena.top_radius) {
					double nx = dvx / len;
					double ny = dvy / len;
					double o2x = corner_ox + nx * (arena.corner_radius - arena.top_radius);
					double o2y = corner_oy + ny * (arena.corner_radius - arena.top_radius);
					dan = min(dan, dan_to_sphere_inner(point, new Vector(o2x, arena.height - arena.top_radius, o2y), arena.top_radius));
				}
			}
		}

		return dan;
	}

	Normal danToArena(Vector point, Arena arena) {
		boolean negateX = point.x < 0;
		boolean negateZ = point.z < 0;
		if (negateX)
			point.x = -point.x;
		if (negateZ)
			point.z = -point.z;
		Normal result = dan_to_arena_quarter(point, arena);
		if (negateX) {
			point.x = -point.x;
			result.normal.x = -result.normal.x;
		}
		if (negateZ) {
			point.z = -point.z;
			result.normal.z = -result.normal.z;
		}
		return result;
	}

	private int tick = -1;
	private int totalDepth = 200;
	private String json = null;
	private Vector ballPredict = null;
	boolean debug = false;
	boolean fileLogging = false;

	// http://mathworld.wolfram.com/Point-LineDistance3-Dimensional.html
	private double pointToLineDist(Vector x1, Vector x2, Vector x0) {
		if (x2.sub(x1).length() == 0)
			return 0;
		return x0.sub(x1).cross(x0.sub(x2)).length() / x2.sub(x1).length();
	}

	class MyAction {
		public Action action;
		public double score;

		public MyAction(Action action, double score) {
			this.action = action;
			this.score = score;
		}
	}

	private Action generateAction(double vx, double vy, double vz, boolean nitro) {
		Action a = new Action();
		a.target_velocity_x = vx;
		a.target_velocity_y = vy;
		a.target_velocity_z = vz;
		a.use_nitro = nitro;
		return a;
	}

	private MyAction FindMove(Rules rules, Arena arena, Entity robot, Entity ball, Vector heuristicMovement, Vector myGoal, Vector opponentGoal, boolean nitro, boolean doMove, int jumpSimDepth, Entity[] opponents) {
		MyAction bestAction = new MyAction(null, -1e9);
		if (nitro && robot.nitro > 0) {
			ArrayList<Action> nitroActions = new ArrayList<Action>();
			nitroActions.add(generateAction(0, 0, 0, false));
			nitroActions.add(generateAction(heuristicMovement.x, robot.position.y, heuristicMovement.z, true));
			nitroActions.add(generateAction(heuristicMovement.x, ball.position.y - robot.position.y, heuristicMovement.z, true));
			nitroActions.add(generateAction(0, rules.NITRO_POINT_VELOCITY_CHANGE, 0, true));
			nitroActions.add(generateAction(robot.velocity.x, robot.velocity.y, robot.velocity.z, true));
			nitroActions.add(generateAction(ball.position.x, ball.position.y, ball.position.z, true));

			for (Action ac : nitroActions) {
				Entity ballEntity = new Entity(ball);
				Entity robotEntity = new Entity(robot);
				double currentScore = ac.use_nitro ? -10 : 0;
				double opponentDist = 1e9;
				for (int i = 0; i < totalDepth; i++) {
					if (i == jumpSimDepth)
						currentScore += 10 * ballEntity.velocity.z;
					update(rules.arena, rules, i < jumpSimDepth ? new Entity[] { robotEntity } : new Entity[] {}, ballEntity, new Action[] { i == 0 ? ac : null });
					for (Entity opp : opponents) {
						opponentDist = Math.min(opponentDist, ballEntity.position.dist(opp.position));
					}
					if (ballEntity.position.z < myGoal.z - ballEntity.radius) {
						currentScore -= 1e6 - 100 * i;
						break;
					}
					if (ballEntity.position.z > opponentGoal.z + ballEntity.radius) {
						currentScore += 1e6 - 100 * i;
						currentScore += 50 * opponentDist; // shoot in the corner or above opponent
						break;
					}
				}
				currentScore -= ballEntity.position.dist(opponentGoal);
				if (currentScore > bestAction.score) {
					bestAction.action = ac;
					bestAction.score = currentScore;
				}
			}

			// nitro high jump
			Entity ballEntity = new Entity(ball);
			Entity robotEntity = new Entity(robot);
			double currentScore = -10 * jumpSimDepth;
			double opponentDist = 1e9;
			Action ac = generateAction(0, 30, 0, true);
			ac.jump_speed = rules.ROBOT_MAX_JUMP_SPEED;
			for (int i = 0; i < totalDepth; i++) {
				if (i == jumpSimDepth)
					currentScore += 10 * ballEntity.velocity.z;
				update(rules.arena, rules, i < jumpSimDepth ? new Entity[] { robotEntity } : new Entity[] {}, ballEntity, new Action[] { ac });
				for (Entity opp : opponents) {
					opponentDist = Math.min(opponentDist, ballEntity.position.dist(opp.position));
				}
				if (ballEntity.position.z < myGoal.z - ballEntity.radius) {
					currentScore -= 1e6 - 100 * i;
					break;
				}
				if (ballEntity.position.z > opponentGoal.z + ballEntity.radius) {
					currentScore += 1e6 - 100 * i;
					currentScore += 50 * opponentDist; // shoot in the corner or above opponent
					break;
				}
			}
			currentScore -= ballEntity.position.dist(opponentGoal);
			if (currentScore > bestAction.score) {
				bestAction.action = ac;
				bestAction.score = currentScore;
			}

			return bestAction;
		}

		if (doMove) {
			for (int angle = 0; angle < 9; angle++) {
				double factor = angle == 8 ? 0 : 10;
				Action a = new Action();
				a.target_velocity_x = heuristicMovement.x + factor * Math.cos(2 * Math.PI * angle / 8);
				a.target_velocity_z = heuristicMovement.z + factor * Math.sin(2 * Math.PI * angle / 8);
				Entity robotEntity = new Entity(robot);
				Entity ballEntity = new Entity(ball);
				update(rules.arena, rules, new Entity[] { robotEntity }, ballEntity, new Action[] { a });
				MyAction result = FindMove(rules, arena, robotEntity, ballEntity, heuristicMovement, myGoal, opponentGoal, false, false, jumpSimDepth - 1, opponents);
				if (result.score > 0) // no patience for hitting my own goal
					result.score *= 0.8;
				result.score -= 1000;
				if (result.score > bestAction.score)
					bestAction = result;
			}
			return bestAction;
		}

		// jump
		for (int jumpSpeed = 0; jumpSpeed <= 20; jumpSpeed++) {
			Action a = new Action();
			a.target_velocity_x = heuristicMovement.x;
			a.target_velocity_z = heuristicMovement.z;
			a.jump_speed = jumpSpeed / 20.0 * rules.ROBOT_MAX_JUMP_SPEED;
			Entity ballEntity = new Entity(ball);
			Entity robotEntity = new Entity(robot);
			if (jumpSpeed > 0 && ballEntity.position.dist(robotEntity.position) > robot.radius + ball.radius + (double) jumpSimDepth / rules.TICKS_PER_SECOND * (rules.ROBOT_MAX_GROUND_SPEED + rules.MAX_ENTITY_SPEED))
				break; // can't reach ball, don't jump
			double currentScore = -1 * jumpSpeed;
			double opponentDist = 1e9;
			for (int i = 0; i < totalDepth; i++) {
				if (i == jumpSimDepth)
					currentScore += 10 * ballEntity.velocity.z;
				update(rules.arena, rules, i < jumpSimDepth ? new Entity[] { robotEntity } : new Entity[] {}, ballEntity, new Action[] { i == 0 ? a : null });
				for (Entity opp : opponents) {
					opponentDist = Math.min(opponentDist, ballEntity.position.dist(opp.position));
				}
				if (ballEntity.position.z < myGoal.z - ballEntity.radius) {
					currentScore -= 1e6 - 100 * i;
					break;
				}
				if (ballEntity.position.z > opponentGoal.z + ballEntity.radius) {
					currentScore += 1e6 - 100 * i;
					currentScore += 50 * opponentDist; // shoot in the corner or above opponent
					break;
				}
				if (ball.position.z < 0 && ballEntity.position.z > 35 && ballEntity.velocity.z < 0) { // bouncing back
					currentScore += 5e5 - 100 * i;
					currentScore += 50 * opponentDist; // shoot in the corner or above opponent
					break;
				}
			}
			currentScore -= ballEntity.position.dist(opponentGoal);
			if (currentScore > bestAction.score) {
				bestAction.action = a;
				bestAction.score = currentScore;
			}
		}
		return bestAction;
	}

	private boolean canReach(Entity robot, Vector ball, Rules rules, int ticks) {
		double oldDelta = deltaTime;
		deltaTime = 1.0 / rules.TICKS_PER_SECOND;
		for (int i = 0; i < ticks; i++) {
			Vector targetVelocity = ball.sub(robot.position);
			targetVelocity = targetVelocity.mul(10).clamp(rules.ROBOT_MAX_GROUND_SPEED);
			Vector target_velocity_change = targetVelocity.sub(robot.velocity);
			robot.velocity = robot.velocity.add(target_velocity_change.normalize().mul(rules.ROBOT_ACCELERATION * deltaTime).clamp(target_velocity_change.length()));
			move(robot, rules);
			if (robot.position.dist(ball) < 3) {
				deltaTime = oldDelta;
				return true;
			}
		}
		deltaTime = oldDelta;
		return false;
	}

	ArrayList<Vector> predict = new ArrayList<>();

	@Override
	public void act(Robot me, Rules rules, Game game, Action action) {
		deltaTime = 1.0 / rules.TICKS_PER_SECOND;
		boolean first = game.current_tick != tick;
		tick = game.current_tick;
		Ball ball = game.ball;

		if (first && fileLogging) {
			StringBuilder turn = new StringBuilder();
			turn.append("ball.x = " + ball.x + ";\n");
			turn.append("ball.y = " + ball.y + ";\n");
			turn.append("ball.z = " + ball.z + ";\n");
			turn.append("ball.velocity_x = " + ball.velocity_x + ";\n");
			turn.append("ball.velocity_y = " + ball.velocity_y + ";\n");
			turn.append("ball.velocity_z = " + ball.velocity_z + ";\n");
			for (int i = 0; i < game.robots.length; i++) {
				turn.append("game.robots[" + i + "].x = " + game.robots[i].x + ";\n");
				turn.append("game.robots[" + i + "].y = " + game.robots[i].y + ";\n");
				turn.append("game.robots[" + i + "].z = " + game.robots[i].z + ";\n");
				turn.append("game.robots[" + i + "].velocity_x = " + game.robots[i].velocity_x + ";\n");
				turn.append("game.robots[" + i + "].velocity_y = " + game.robots[i].velocity_y + ";\n");
				turn.append("game.robots[" + i + "].velocity_z = " + game.robots[i].velocity_z + ";\n");
				turn.append("game.robots[" + i + "].is_teammate = " + game.robots[i].is_teammate + ";\n");
				if (game.robots[i] == me)
					turn.append("me = game.robots[" + i + "];\n");
			}
			try (PrintWriter out = new PrintWriter("logs/" + game.current_tick + ".txt")) {
				out.println(turn.toString());
			} catch (FileNotFoundException e) {
				e.printStackTrace();
			}
		}

		Vector ballPoint = new Vector(ball.x, ball.y, ball.z);
		Vector myGoal = new Vector(0, 0, -rules.arena.depth / 2);
		Vector opponentGoal = new Vector(0, 0, rules.arena.depth / 2);
		Vector myPoint = new Vector(me.x, me.y, me.z);

		Vector closestToBall = myPoint;
		for (Robot r : game.robots) {
			Vector tmp = new Vector(r.x, r.y, r.z);
			if (tmp.dist(ballPoint) < closestToBall.dist(ballPoint))
				closestToBall = tmp;
		}

		if (first) {
			predict.clear();
			Entity ballEntity = new Entity(game.ball, rules);
			for (int i = 0; i < totalDepth; i++) {
				update(rules.arena, rules, new Entity[] {}, ballEntity, new Action[] { i == 0 ? action : null });
				predict.add(new Vector(ballEntity.position.x, ballEntity.position.y, ballEntity.position.z));
				if (ballEntity.position.z < myGoal.z - ballEntity.radius) {
					break;
				}
				if (ballEntity.position.z > opponentGoal.z + ballEntity.radius) {
					break;
				}
			}
			if (ballPredict != null && json != null) {
				json += " {\r\n" + "    \"Sphere\": {\r\n" + "      \"x\": " + ballPredict.x + ",\r\n" + "      \"y\": " + ballPredict.y + ",\r\n" + "      \"z\": " + ballPredict.z + ",\r\n" + "      \"radius\": " + ball.radius + ",\r\n" + "      \"r\": 1.0,\r\n" + "      \"g\": 1.0,\r\n" + "      \"b\": 1.0,\r\n" + "      \"a\": 0.5\r\n" + "    }\r\n" + "  },";
			}
			ballPredict = predict.get(0);
		}

		Entity[] opponents = new Entity[game.robots.length / 2];
		int opponentIndex = 0;
		boolean defender = true;
		boolean striker = true;
		Robot teammate = null;
		for (Robot r : game.robots) {
			if (r.is_teammate && r != me)
				teammate = r;
			if (r.is_teammate && r.z < me.z)
				defender = false;
			if (r.is_teammate && r.z > me.z)
				striker = false;
			if (!r.is_teammate)
				opponents[opponentIndex++] = new Entity(r, rules);
		}
		boolean midfielder = !defender && !striker;
		Entity closestOpponent = opponents[0];
		for (Entity r : opponents) {
			if (r.position.dist(ballPoint) < closestOpponent.position.dist(ballPoint))
				closestOpponent = r;
		}

		int eta = 0;
		if (myPoint.dist(ballPoint) > 2 * ball.radius) { // can't reach ball, go to predicted landing point
			int steps = 0;
			for (Vector p : predict) {
				steps++;
				if (p.y < 2.5 * ball.radius && canReach(new Entity(me, rules), p, rules, steps)) {// p.dist(myPoint) < deltaTime * rules.ROBOT_MAX_GROUND_SPEED * steps) {
					ballPoint = p;
					eta = steps;
					break;
				}
			}
		}

		double factor = 0.7;
		Vector goal = new Vector(opponentGoal.x, opponentGoal.y, opponentGoal.z + 40);
		while (true) {
			double goalX = ball.x + (goal.x - ball.x) * (rules.arena.depth / 2 - ball.z) / (goal.z - ball.z);
			if (Math.abs(goalX) < rules.arena.goal_width / 2 - ball.radius)
				break;
			goal.z--;
		}

		if (defender) {
			boolean willHitMyGoal = false;
			boolean low = true; // buggy, but don't touch it
			boolean allHigh = ball.z < 0;
			boolean checkLow = true;
			int steps = 0;
			for (Vector v : predict) {
				if (checkLow && canReach(new Entity(me, rules), v, rules, steps))
					checkLow = false;
				if (checkLow && v.z > ball.radius + 1)
					low = false;
				if (v.y < 5)
					allHigh = false;
				if (v.z < myGoal.z - ball.radius) {
					willHitMyGoal = true;
					break;
				}
			}

			boolean mustBlock = true;
			for (Robot r : game.robots) {
				if (r.is_teammate && r != me && r.z < ball.z)
					mustBlock = false;
			}
			if (!allHigh && mustBlock && (low && willHitMyGoal) || (!willHitMyGoal && ball.z < -30) || (low && ball.z < -20 && !willHitMyGoal) && ball.velocity_z < -10) {
				defender = false;
			}
			if (allHigh) {
				for (Vector v : predict) {
					if (v.z > -38)
						ballPoint = v;
					if (v.z < myGoal.z - ball.radius) {
						break;
					}
				}
			}
		}

		if (myPoint.dist(myGoal) > ballPoint.dist(myGoal)) {
			factor = -1.7;
			goal = myGoal;
		}
		Vector goalToBall = goal.to(ballPoint);
		double dist = goalToBall.length();
		goalToBall.scaleTo(dist + factor * rules.BALL_RADIUS);
		Vector target = goalToBall.add(goal);
		if (defender) {
			target = new Vector(myGoal.x, myGoal.y, myGoal.z);
			if (predict.size() > 1 && predict.get(predict.size() - 1).z < -rules.arena.depth / 2) {
				int index = predict.size() - 2;
				while (index > 0 && predict.get(index).y > 2 * me.radius)
					index--;
				if (predict.get(index).y > 2 * me.radius) {
					index = predict.size() - 2;
					while (index > 0 && predict.get(index).z < -38)
						index--;
				}
				target = predict.get(index);
				if (target.x > 0)
					target = new Vector(target.x - 0.3, target.y, target.z);
				else
					target = new Vector(target.x + 0.3, target.y, target.z);
				if (index != predict.size() - 2)
					target.z += 0.5;

				boolean pathOnLine = true;
				boolean meOnPath = false;
				for (Vector v : predict) {
					if (pointToLineDist(ballPoint, predict.get(predict.size() - 1), v) > 0.1)
						pathOnLine = false;
					if (v.y > ball.radius + 0.2)
						pathOnLine = false;
					if (myPoint.dist(v) < ball.radius)
						meOnPath = true;
				}
				if (meOnPath && myPoint == closestToBall && pathOnLine) {
					target = ballPoint;
				}
			} else {
				Vector opp = closestOpponent.position;
				double goalX = ball.x * 2 / 3;

				if (goalX > rules.arena.goal_width / 2 - 2 - 0.1 * me.velocity_x)
					goalX = rules.arena.goal_width / 2 - 2 - 0.1 * me.velocity_x;
				if (goalX < -(rules.arena.goal_width / 2 - 2 - 0.1 * me.velocity_x))
					goalX = -(rules.arena.goal_width / 2 - 2 - 0.1 * me.velocity_x);
				target = new Vector(goalX, 0, myGoal.z + 1);

				if (false && ball.velocity_z < -15 && myPoint == closestToBall) {
					int steps = 0;
					for (Vector p : predict) {
						steps++;
						if (p.y < 1.5 * ball.radius && canReach(new Entity(me, rules), p, rules, steps)) {// p.dist(myPoint) < deltaTime * rules.ROBOT_MAX_GROUND_SPEED * steps) {
							target = new Vector(p.x, p.y, p.z - 0.5);
							System.err.println("keeper rush at " + game.current_tick);
							break;
						}
					}
				}
			}
		}

		if (!defender && target.z < me.z && ball.z < ball.radius + me.radius) {
			// will i run towards my own goal and possibly score on the wrong side?
			double goalX = me.x + (target.x - me.x) * (myGoal.z - me.z) / (target.z - me.z);
			if (goalX > 0 && goalX < rules.arena.goal_width / 2)
				target.x -= ball.radius;
			else if (goalX < 0 && goalX > -rules.arena.goal_width / 2)
				target.x += ball.radius;

		}

		if (game.robots.length == 6) {
			if (striker && target.z < 0)
				target.z = 0;
			if (midfielder && target.z > 0)
				target.z = 0;
		}

		if (!defender) {
			for (Robot r : game.robots) {
				if (r.is_teammate && r != me && new Entity(r, rules).position.dist(target) < 5 && new Entity(r, rules).position.dist(target) < new Entity(me, rules).position.dist(target)) {
					if (target.z < 0)
						target.z += 5;
					else
						target.z -= 5;
				}
			}
		}

		Vector movement = myPoint.to(target);
		movement.y = 0;
		target.y = 0;
		double bestScale = 0.3;
		Vector closest = myPoint;
		for (double scale = -0.5; eta > 0 && scale <= 0.5; scale += 0.01) {
			Entity dummy = new Entity(me, rules);
			for (int i = 0; i < eta && i < 10; i++) {
				Vector targetVelocity = movement.mul(scale * rules.TICKS_PER_SECOND);
				targetVelocity = targetVelocity.clamp(rules.ROBOT_MAX_GROUND_SPEED);
				Vector target_velocity_change = targetVelocity.sub(dummy.velocity);
				if (target_velocity_change.length() > 0) {
					dummy.velocity = dummy.velocity.add(target_velocity_change.normalize().mul(deltaTime).clamp(target_velocity_change.length()));
				}
				move(dummy, rules);
			}
			if (closest.dist(target) > dummy.position.dist(target)) {
				closest = dummy.position;
				bestScale = scale;
			}
		}
		movement = movement.mul(bestScale * rules.TICKS_PER_SECOND);
		movement.y = 0;
		if (json != null)
			json += " {\r\n" + "    \"Sphere\": {\r\n" + "      \"x\": " + target.x + ",\r\n" + "      \"y\": " + target.y + ",\r\n" + "      \"z\": " + target.z + ",\r\n" + "      \"radius\": 0.3,\r\n" + "      \"r\": 1.0,\r\n" + "      \"g\": 0.0,\r\n" + "      \"b\": 1.0,\r\n" + "      \"a\": 0.5\r\n" + "    }\r\n" + "  },";

		int jumpSimDepth = defender ? 10 : 7;
		MyAction plan = FindMove(rules, rules.arena, new Entity(me, rules), new Entity(ball, rules), movement, myGoal, opponentGoal, false, false, jumpSimDepth, opponents);
		if (!me.touch && me.nitro_amount > 0) {
			MyAction movePlan = FindMove(rules, rules.arena, new Entity(me, rules), new Entity(ball, rules), movement, myGoal, opponentGoal, true, false, jumpSimDepth, opponents);
			if (movePlan.score > plan.score) {
				plan = movePlan;
			}
		}
		ballPoint = new Vector(ball.x, ball.y, ball.z);
		if (false && myPoint.dist(ballPoint) < 7 && closestOpponent.position.dist(ballPoint) > 10) {
			MyAction movePlan = FindMove(rules, rules.arena, new Entity(me, rules), new Entity(ball, rules), movement, myGoal, opponentGoal, true, false, jumpSimDepth, opponents);
			if (movePlan.score > plan.score) {
				plan = movePlan;
			}
		}
		if (!defender && plan.score < 0 && me.touch) {
			jumpSimDepth = 20;
			totalDepth = 50;
			MyAction jumpPlan = FindMove(rules, rules.arena, new Entity(me, rules), new Entity(ball, rules), movement, myGoal, opponentGoal, false, false, jumpSimDepth, opponents);
			if (jumpPlan.score > plan.score && jumpPlan.score > 0) {
				//if (jumpPlan.action.jump_speed > plan.action.jump_speed)
				//	System.err.println("high jump attack at " + game.current_tick);
				plan = jumpPlan;
			}
			totalDepth = 200;
		}
		if (defender && plan.score < -1e5 && me.touch) {
			jumpSimDepth = 20;
			totalDepth = 50;
			MyAction jumpPlan = FindMove(rules, rules.arena, new Entity(me, rules), new Entity(ball, rules), movement, myGoal, opponentGoal, true, false, jumpSimDepth, opponents);
			if (jumpPlan.score > plan.score && jumpPlan.score > -1e5) {
				//if (jumpPlan.action.jump_speed > plan.action.jump_speed)
				//	System.err.println("high jump defense at " + game.current_tick);
				plan = jumpPlan;
			}
			totalDepth = 200;
		}
		if (plan.action == null)
			plan.action = action;
		if (json != null)
			json += "{\r\n" + "    \"Text\": \"score: " + plan.score + "\"\r\n" + "  },";
		action.target_velocity_x = plan.action.target_velocity_x;
		action.target_velocity_y = plan.action.target_velocity_y;
		action.target_velocity_z = plan.action.target_velocity_z;
		action.jump_speed = plan.action.jump_speed;
		action.use_nitro = plan.action.use_nitro;

		if (json != null) {
			Entity ballEntity = new Entity(ball, rules);
			Entity robotEntity = new Entity(me, rules);
			for (int i = 0; i < totalDepth; i++) {
				update(rules.arena, rules, i < jumpSimDepth ? new Entity[] { robotEntity } : new Entity[] {}, ballEntity, new Action[] { i == 0 ? action : null });
				json += " {\r\n" + "    \"Sphere\": {\r\n" + "      \"x\": " + ballEntity.position.x + ",\r\n" + "      \"y\": " + ballEntity.position.y + ",\r\n" + "      \"z\": " + ballEntity.position.z + ",\r\n" + "      \"radius\": 0.3,\r\n" + "      \"r\": 1.0,\r\n" + "      \"g\": 1.0,\r\n" + "      \"b\": 1.0,\r\n" + "      \"a\": 0.5\r\n" + "    }\r\n" + "  },";
				if (i < jumpSimDepth)
					json += " {\r\n" + "    \"Sphere\": {\r\n" + "      \"x\": " + robotEntity.position.x + ",\r\n" + "      \"y\": " + robotEntity.position.y + ",\r\n" + "      \"z\": " + robotEntity.position.z + ",\r\n" + "      \"radius\": 0.3,\r\n" + "      \"r\": 0.0,\r\n" + "      \"g\": 1.0,\r\n" + "      \"b\": 0.0,\r\n" + "      \"a\": 0.5\r\n" + "    }\r\n" + "  },";
			}
		}
	}

	@Override
	public String customRendering() {
		if (json == null)
			return "";
		String result = "[" + json.substring(0, json.length() - 1) + "]";
		json = "";
		return result;
	}
}