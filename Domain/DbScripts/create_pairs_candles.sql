USE candleswap;

CREATE TABLE pairs(
    id BIGINT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
    token0Id VARBINARY(20) NOT NULL,
    token1Id VARBINARY(20) NOT NULL
);

ALTER TABLE pairs
ADD CONSTRAINT UC_pairs UNIQUE(token0Id, token1Id);

CREATE TABLE candles(
    id BIGINT NOT NULL IDENTITY(1, 1) PRIMARY KEY,
    datetime BIGINT NOT NULL,
    resolutionSeconds INT NOT NULL,
    pairId BIGINT NOT NULL,
    [open] VARCHAR(78) NOT NULL,
    high VARCHAR(78) NOT NULL,
    low VARCHAR(78) NOT NULL,
    [close] VARCHAR(78) NOT NULL,
    volume INTEGER NOT NULL
);

ALTER TABLE candles
ADD CONSTRAINT UC_candles UNIQUE(pairId, datetime, resolutionSeconds);

ALTER TABLE candles
ADD CONSTRAINT FK_candles FOREIGN KEY(pairId) REFERENCES pairs(id);